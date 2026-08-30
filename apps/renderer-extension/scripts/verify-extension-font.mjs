import { spawn } from "node:child_process";
import { mkdir, mkdtemp, readFile, readdir, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

const repositoryRoot = resolve(import.meta.dirname, "../../..");
const chromeRoot = join(repositoryRoot, "artifacts", "tooling", "chrome-for-testing");
const extensionRootArgument = process.argv.indexOf("--extension-root");
const extensionRoot = extensionRootArgument >= 0
  ? resolve(process.argv[extensionRootArgument + 1] ?? "")
  : join(repositoryRoot, "artifacts", "renderer-extension", "unpacked");
if (extensionRootArgument >= 0 && !process.argv[extensionRootArgument + 1]) {
  throw new Error("--extension-root requires a path");
}
const evidenceRoot = join(repositoryRoot, "artifacts", "renderer-extension", "evidence", "extension-font");
const versions = (await readdir(chromeRoot, { withFileTypes: true }))
  .filter(entry => entry.isDirectory())
  .map(entry => entry.name)
  .sort((left, right) => right.localeCompare(left, undefined, { numeric: true }));
if (versions.length === 0) throw new Error("Chrome for Testing is not installed");
const chrome = join(chromeRoot, versions[0], "chrome-win64", "chrome.exe");
const profile = await mkdtemp(join(tmpdir(), "modern-renderer-extension-font-"));
const fixtureHtml = `<!doctype html>
<html lang="th"><head><meta charset="utf-8"><title>Instagram font fixture</title></head>
<body style="font: 48px/1.5 'Segoe UI Emoji', sans-serif">
  <main id="transcript">ทดสอบ 🫩 🫯 🤍 ❤️ 👩🏽‍💻 👨‍👩‍👧‍👦 1️⃣ 🇹🇭</main>
  <section id="story-reply">ตอบสตอรี่
    <img id="instagram-emoji" height="16" width="16" alt="🥺" src="https://static.cdninstagram.com/images/emoji.php/v9/t73/1/16/1f979.png">
    <img id="ordinary-image" alt="🥺" src="data:image/gif;base64,R0lGODlhAQABAAAAACw=">
  </section>
</body></html>`;

await rm(evidenceRoot, { recursive: true, force: true });
await mkdir(evidenceRoot, { recursive: true });
const browser = spawn(chrome, [
  "--headless=new",
  "--no-first-run",
  "--no-default-browser-check",
  "--remote-debugging-port=0",
  `--user-data-dir=${profile}`,
  `--load-extension=${extensionRoot}`,
  `--disable-extensions-except=${extensionRoot}`,
  "about:blank",
], { stdio: "ignore", windowsHide: true });

try {
  let port;
  for (let attempt = 0; attempt < 100; attempt += 1) {
    try {
      port = (await readFile(join(profile, "DevToolsActivePort"), "utf8")).split(/\r?\n/u)[0];
      break;
    } catch {
      await new Promise(resolveWait => setTimeout(resolveWait, 100));
    }
  }
  if (!port) throw new Error("Chrome did not publish DevToolsActivePort");
  const targets = await fetch(`http://127.0.0.1:${port}/json/list`).then(response => response.json());
  const page = targets.find(target => target.type === "page");
  if (!page) throw new Error("Page target not found");
  const socket = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise((open, error) => {
    socket.addEventListener("open", open, { once: true });
    socket.addEventListener("error", error, { once: true });
  });

  let nextId = 0;
  const pending = new Map();
  const fontNetworkEvents = [];
  const send = (method, params = {}) => new Promise((resolveMessage, reject) => {
    const id = ++nextId;
    pending.set(id, { resolve: resolveMessage, reject });
    socket.send(JSON.stringify({ id, method, params }));
  });
  socket.addEventListener("message", event => {
    const message = JSON.parse(event.data);
    if (message.id && pending.has(message.id)) {
      const request = pending.get(message.id);
      pending.delete(message.id);
      if (message.error) request.reject(new Error(message.error.message));
      else request.resolve(message.result);
      return;
    }
    if (message.method === "Fetch.requestPaused") {
      const { requestId, resourceType, request } = message.params;
      if (/Noto-COLRv1\.ttf/iu.test(request.url)) {
        fontNetworkEvents.push({ method: message.method, resourceType, url: request.url });
      }
      if (resourceType === "Document") {
        void send("Fetch.fulfillRequest", {
          requestId,
          responseCode: 200,
          responseHeaders: [{ name: "Content-Type", value: "text/html; charset=utf-8" }],
          body: Buffer.from(fixtureHtml).toString("base64"),
        });
      } else if (resourceType === "Image" && /cdninstagram\.com\/images\/emoji\.php\//iu.test(request.url)) {
        void send("Fetch.fulfillRequest", {
          requestId,
          responseCode: 200,
          responseHeaders: [{ name: "Content-Type", value: "image/png" }],
          body: "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=",
        });
      } else {
        void send("Fetch.failRequest", { requestId, errorReason: "BlockedByClient" });
      }
    }
    if (message.method === "Network.requestWillBeSent" && /Noto-COLRv1\.ttf/iu.test(message.params.request.url)) {
      fontNetworkEvents.push({ method: message.method, url: message.params.request.url });
    }
    if (message.method === "Network.loadingFailed" && /font/iu.test(message.params.type ?? "")) {
      fontNetworkEvents.push({
        method: message.method,
        errorText: message.params.errorText,
        blockedReason: message.params.blockedReason ?? null,
        corsErrorStatus: message.params.corsErrorStatus ?? null,
      });
    }
  });

  await send("Page.enable");
  await send("Network.enable");
  await send("DOM.enable");
  await send("CSS.enable");
  await send("Fetch.enable", {
    patterns: [
      { urlPattern: "https://www.instagram.com/*", requestStage: "Request" },
      { urlPattern: "https://*.cdninstagram.com/images/emoji.php/*", requestStage: "Request" },
    ],
  });
  await send("Page.navigate", { url: "https://www.instagram.com/direct/t/renderer-font-fixture" });

  let state;
  for (let attempt = 0; attempt < 100; attempt += 1) {
    const evaluation = await send("Runtime.evaluate", {
      expression: `(() => {
        const wrapper = document.querySelector('[data-modern-emoji-renderer="emoji-image"]');
        return {
          url: location.href,
          wrapperCount: document.querySelectorAll('[data-modern-emoji-renderer]').length,
          textWrapperCount: document.querySelectorAll('[data-modern-emoji-renderer="emoji"]').length,
          imageWrapperCount: document.querySelectorAll('[data-modern-emoji-renderer="emoji-image"]').length,
          imageWrapperText: wrapper?.textContent ?? null,
          sourceImageHidden: document.querySelector('#instagram-emoji')?.hidden ?? null,
          ordinaryImageHidden: document.querySelector('#ordinary-image')?.hidden ?? null,
          computedFontFamily: wrapper ? getComputedStyle(wrapper).fontFamily : null,
          fontFaces: [...document.fonts].map(face => ({ family: face.family, status: face.status })),
        };
      })()`,
      returnByValue: true,
    });
    state = evaluation.result.value;
    if (state.textWrapperCount > 0 && state.imageWrapperCount === 1) break;
    await new Promise(resolveWait => setTimeout(resolveWait, 100));
  }
  if (!state || state.wrapperCount === 0) throw new Error("Extension did not create Emoji wrappers");
  await send("Runtime.evaluate", {
    expression: `(async () => {
      await document.fonts.ready;
      await new Promise(resolveFrame => requestAnimationFrame(() => requestAnimationFrame(resolveFrame)));
      document.querySelector('[data-modern-emoji-renderer="emoji-image"]').getBoundingClientRect();
    })()`,
    awaitPromise: true,
  });

  const documentNode = await send("DOM.getDocument", { depth: 1 });
  const wrapperNode = await send("DOM.querySelector", {
    nodeId: documentNode.root.nodeId,
    selector: '[data-modern-emoji-renderer="emoji-image"]',
  });
  if (!wrapperNode.nodeId) throw new Error("Wrapped Emoji node was not found through CDP DOM");
  const platformFonts = await send("CSS.getPlatformFontsForNode", { nodeId: wrapperNode.nodeId });
  const finalState = await send("Runtime.evaluate", {
    expression: `(() => ({
      wrapperCount: document.querySelectorAll('[data-modern-emoji-renderer]').length,
      textWrapperCount: document.querySelectorAll('[data-modern-emoji-renderer="emoji"]').length,
      imageWrapperCount: document.querySelectorAll('[data-modern-emoji-renderer="emoji-image"]').length,
      imageWrapperText: document.querySelector('[data-modern-emoji-renderer="emoji-image"]')?.textContent ?? null,
      sourceImageHidden: document.querySelector('#instagram-emoji')?.hidden ?? null,
      ordinaryImageHidden: document.querySelector('#ordinary-image')?.hidden ?? null,
      computedFontFamily: getComputedStyle(document.querySelector('[data-modern-emoji-renderer="emoji-image"]')).fontFamily,
      fontFaces: [...document.fonts].map(face => ({ family: face.family, status: face.status })),
    }))()`,
    returnByValue: true,
  });
  const report = {
    chromeVersion: versions[0],
    extensionArtifact: extensionRoot.endsWith(`${join("renderer-extension", "unpacked")}`)
      ? "development-unpacked"
      : "release-package",
    page: "https://www.instagram.com/direct/t/<fixture>",
    ...finalState.result.value,
    platformFonts: platformFonts.fonts,
    fontNetworkEvents,
  };
  await writeFile(join(evidenceRoot, "report.json"), `${JSON.stringify(report, null, 2)}\n`, "utf8");
  socket.close();

  const usesBundledNoto = report.platformFonts.some(font => (
    font.isCustomFont && /noto.*emoji/iu.test(`${font.familyName} ${font.postScriptName}`)
  ));
  if (!usesBundledNoto) {
    throw new Error(`Wrapped Emoji did not render with bundled Noto: ${JSON.stringify(report.platformFonts)}`);
  }
  if (
    report.imageWrapperCount !== 1
    || report.imageWrapperText !== "🥺"
    || report.sourceImageHidden !== true
    || report.ordinaryImageHidden !== false
  ) {
    throw new Error(`Instagram image Emoji boundary failed: ${JSON.stringify(report)}`);
  }
  const pageOriginFontRequest = report.fontNetworkEvents.find(event => (
    typeof event.url === "string" && event.url.startsWith("https://www.instagram.com/")
  ));
  if (pageOriginFontRequest) {
    throw new Error(`Bundled font resolved against the page origin: ${pageOriginFontRequest.url}`);
  }
  process.stdout.write(`Extension bundled-font verification passed\n${evidenceRoot}\n`);
} finally {
  browser.kill();
  if (browser.exitCode === null) await new Promise(resolveExit => browser.once("exit", resolveExit));
  await rm(profile, { recursive: true, force: true });
}
