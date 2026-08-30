import { mkdir, mkdtemp, readFile, readdir, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { spawn } from "node:child_process";

const root = resolve(import.meta.dirname, "../../..");
const chromeRoot = join(root, "artifacts", "tooling", "chrome-for-testing");
const extensionRoot = join(root, "artifacts", "renderer-extension", "unpacked");
const evidence = join(root, "artifacts", "renderer-extension", "evidence", "tickets-07-08");
const versions = (await readdir(chromeRoot, { withFileTypes: true }))
  .filter(entry => entry.isDirectory()).map(entry => entry.name)
  .sort((left, right) => right.localeCompare(left, undefined, { numeric: true }));
if (versions.length === 0) throw new Error("Chrome for Testing is not installed");
const chrome = join(chromeRoot, versions[0], "chrome-win64", "chrome.exe");
const profile = await mkdtemp(join(tmpdir(), "modern-emoji-ui-fixture-"));
await rm(evidence, { recursive: true, force: true });
await mkdir(evidence, { recursive: true });
const browser = spawn(chrome, [
  "--headless=new", "--no-first-run", "--no-default-browser-check", "--remote-debugging-port=0",
  `--user-data-dir=${profile}`, `--disable-extensions-except=${extensionRoot}`, `--load-extension=${extensionRoot}`,
  "about:blank"
], { stdio: "ignore", windowsHide: true });

async function connect(webSocketDebuggerUrl) {
  const socket = new WebSocket(webSocketDebuggerUrl);
  await new Promise((open, error) => {
    socket.addEventListener("open", open, { once: true });
    socket.addEventListener("error", error, { once: true });
  });
  let nextId = 0;
  const pending = new Map();
  socket.addEventListener("message", event => {
    const message = JSON.parse(event.data);
    if (!message.id || !pending.has(message.id)) return;
    const request = pending.get(message.id);
    pending.delete(message.id);
    if (message.error) request.reject(new Error(message.error.message)); else request.resolve(message.result);
  });
  const send = (method, params = {}) => new Promise((resolveMessage, reject) => {
    const id = ++nextId;
    pending.set(id, { resolve: resolveMessage, reject });
    socket.send(JSON.stringify({ id, method, params }));
  });
  return { socket, send };
}

async function evaluate(send, expression, options = {}) {
  const result = await send("Runtime.evaluate", {
    expression, awaitPromise: true, returnByValue: true, ...options,
  });
  if (result.exceptionDetails) {
    throw new Error(result.exceptionDetails.exception?.description ?? result.exceptionDetails.text);
  }
  return result.result.value;
}

async function waitFor(send, expression) {
  return evaluate(send, `(async () => {
    for (let attempt = 0; attempt < 120; attempt += 1) {
      const value = (${expression});
      if (value) return value;
      await new Promise(resolve => setTimeout(resolve, 25));
    }
    throw new Error("UI condition timed out: ${expression.replaceAll('"', '\\"')}");
  })()`);
}

async function capture(send, name, colorScheme, width, height) {
  await send("Emulation.setDeviceMetricsOverride", { width, height, deviceScaleFactor: 1, mobile: false });
  await send("Emulation.setEmulatedMedia", {
    media: "screen", features: [{ name: "prefers-color-scheme", value: colorScheme }],
  });
  const image = await send("Page.captureScreenshot", { format: "png", fromSurface: true, captureBeyondViewport: true });
  await writeFile(join(evidence, name), Buffer.from(image.data, "base64"));
}

try {
  let port;
  for (let attempt = 0; attempt < 100; attempt += 1) {
    try { port = (await readFile(join(profile, "DevToolsActivePort"), "utf8")).split(/\r?\n/u)[0]; break; }
    catch { await new Promise(done => setTimeout(done, 100)); }
  }
  if (!port) throw new Error("Chrome did not publish DevToolsActivePort");
  let serviceWorker;
  for (let attempt = 0; attempt < 100; attempt += 1) {
    const targets = await fetch(`http://127.0.0.1:${port}/json/list`).then(response => response.json());
    serviceWorker = targets.find(target => target.type === "service_worker" && target.url.endsWith("/background/service-worker.js"));
    if (serviceWorker) break;
    await new Promise(done => setTimeout(done, 100));
  }
  if (!serviceWorker) throw new Error("Renderer extension service worker not found");
  const extensionUrl = new URL(serviceWorker.url);
  const extensionBase = `${extensionUrl.protocol}//${extensionUrl.hostname}`;
  const browserTarget = await fetch(`http://127.0.0.1:${port}/json/version`).then(response => response.json());
  const browserConnection = await connect(browserTarget.webSocketDebuggerUrl);

  const openExtensionPage = async path => {
    const created = await browserConnection.send("Target.createTarget", { url: `${extensionBase}/${path}` });
    let target;
    for (let attempt = 0; attempt < 100; attempt += 1) {
      const targets = await fetch(`http://127.0.0.1:${port}/json/list`).then(response => response.json());
      target = targets.find(candidate => candidate.id === created.targetId);
      if (target) break;
      await new Promise(done => setTimeout(done, 25));
    }
    if (!target) throw new Error(`Extension UI target not found: ${path}`);
    const connection = await connect(target.webSocketDebuggerUrl);
    await connection.send("Page.enable");
    await waitFor(connection.send, "document.readyState === 'complete'");
    return connection;
  };

  const options = await openExtensionPage("options/options.html");
  const initialization = await waitFor(options.send,
    "document.querySelector('#sites')?.value.includes('instagram.com') ? 'ready' : document.querySelector('#form-status')?.textContent");
  if (initialization !== "ready") throw new Error(`Options initialization failed: ${initialization}`);
  const optionsState = await evaluate(options.send, `(() => ({
    enabled: document.querySelector('#enabled').checked,
    mode: document.querySelector('#mode').value,
    sites: document.querySelector('#sites').value.split('\\n'),
    debug: document.querySelector('#debug').checked,
    rendererMode: document.querySelector('#renderer-mode').value,
    version: document.querySelector('#extension-version').textContent,
    labeledControls: [...document.querySelectorAll('input, select, textarea')].every(control => control.labels?.length),
    language: document.documentElement.lang
  }))()`);
  if (!optionsState.enabled || optionsState.mode !== "allowlist" || optionsState.debug ||
      optionsState.rendererMode !== "noto-colrv1" || optionsState.language !== "th" || !optionsState.labeledControls ||
      JSON.stringify(optionsState.sites) !== JSON.stringify(["instagram.com", "tiktok.com"])) {
    throw new Error(`Options defaults/accessibility failed: ${JSON.stringify(optionsState)}`);
  }
  await evaluate(options.send, `(() => {
    document.querySelector('#debug').checked = true;
    document.querySelector('#settings-form').requestSubmit();
    return true;
  })()`, { userGesture: true });
  await waitFor(options.send, "document.querySelector('#form-status')?.textContent.includes('บันทึก')");
  const saved = await evaluate(options.send, "chrome.storage.local.get('rendererSettings')");
  if (saved.rendererSettings?.debug !== true) throw new Error("Options did not persist debug mode");
  await evaluate(options.send, "(document.querySelector('#reset').click(), true)", { userGesture: true });
  await waitFor(options.send, "document.querySelector('#form-status')?.textContent.includes('คืนค่า')");
  const reset = await evaluate(options.send, "chrome.storage.local.get('rendererSettings')");
  if (reset.rendererSettings?.debug !== false) throw new Error("Options reset did not restore defaults");
  await capture(options.send, "options-light.png", "light", 900, 850);
  await capture(options.send, "options-dark.png", "dark", 900, 850);
  options.socket.close();

  const popup = await openExtensionPage("popup/popup.html");
  await waitFor(popup.send, "document.querySelector('#site-enabled')?.disabled === true");
  const popupState = await evaluate(popup.send, `(() => ({
    language: document.documentElement.lang,
    toggleLabel: document.querySelector('#site-enabled').labels?.[0]?.textContent.trim(),
    optionsText: document.querySelector('#open-options').textContent.trim(),
    liveRegion: document.querySelector('#site-status').getAttribute('aria-live'),
    restricted: document.querySelector('#site').textContent
  }))()`);
  if (popupState.language !== "th" || !popupState.toggleLabel || !popupState.optionsText ||
      popupState.liveRegion !== "polite" || popupState.restricted !== "หน้านี้ไม่รองรับ") {
    throw new Error(`Popup accessibility/restricted state failed: ${JSON.stringify(popupState)}`);
  }
  await capture(popup.send, "popup-light.png", "light", 360, 420);
  await capture(popup.send, "popup-dark.png", "dark", 360, 420);
  popup.socket.close();
  browserConnection.socket.close();

  const report = { chromeVersion: versions[0], extensionBase, optionsState, popupState, saveResetPassed: true };
  await writeFile(join(evidence, "report.json"), `${JSON.stringify(report, null, 2)}\n`);
  process.stdout.write(`Renderer popup/options UI fixture passed\n${evidence}\n`);
} finally {
  browser.kill();
  if (browser.exitCode === null) await new Promise(done => browser.once("exit", done));
  await rm(profile, { recursive: true, force: true });
}
