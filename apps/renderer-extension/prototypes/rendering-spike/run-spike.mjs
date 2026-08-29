import { copyFile, mkdir, mkdtemp, readFile, readdir, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { spawn } from "node:child_process";

const root = resolve(import.meta.dirname, "../../../..");
const output = join(root, "artifacts", "renderer-extension", "rendering-spike");
const tooling = join(root, "artifacts", "tooling");
const fontSource = join(tooling, "noto-rendering-spike-v2.051");
const chromeRoot = join(tooling, "chrome-for-testing");
const htmlSource = join(import.meta.dirname, "rendering-spike.html");
const vendor = join(root, "vendor", "noto-emoji", "v2.051");

const assets = [
  ["emoji_u1fae9.png", "png/{size}/emoji_u1fae9.png"],
  ["emoji_u2764.png", "png/{size}/emoji_u2764.png"],
  ["emoji_u1f44c_1f3fb.png", "png/{size}/emoji_u1f44c_1f3fb.png"],
  ["emoji_u1f469_200d_1f4bb.png", "png/{size}/emoji_u1f469_200d_1f4bb.png"],
  ["emoji_u1f468_200d_1f469_200d_1f467_200d_1f466.png", "png/{size}/emoji_u1f468_200d_1f469_200d_1f467_200d_1f466.png"],
  ["emoji_u0031_20e3.png", "png/{size}/emoji_u0031_20e3.png"],
  ["TH.png", "third_party/region-flags/png/TH.png"],
  ["GB-ENG.png", "third_party/region-flags/png/GB-ENG.png"],
  ["emoji_u1faef.png", "png/{size}/emoji_u1faef.png"]
];

await rm(output, { recursive: true, force: true });
await mkdir(output, { recursive: true });
await copyFile(htmlSource, join(output, "index.html"));
for (const font of ["NotoColorEmoji.ttf", "NotoColorEmoji_WindowsCompatible.ttf", "Noto-COLRv1.ttf"]) {
  await copyFile(join(fontSource, font), join(output, font));
}
for (const size of ["128", "512"]) {
  const target = join(output, "images", size);
  await mkdir(target, { recursive: true });
  for (const [name, pattern] of assets) {
    const relative = pattern.replace("{size}", size);
    await copyFile(join(vendor, relative), join(target, name));
  }
}

const chromeVersions = (await readdir(chromeRoot, { withFileTypes: true }))
  .filter(entry => entry.isDirectory())
  .map(entry => entry.name)
  .sort((left, right) => right.localeCompare(left, undefined, { numeric: true }));
if (chromeVersions.length === 0) throw new Error("Chrome for Testing is not installed");
const chrome = join(chromeRoot, chromeVersions[0], "chrome-win64", "chrome.exe");
const profile = await mkdtemp(join(tmpdir(), "modern-emoji-rendering-spike-"));
const browser = spawn(chrome, [
  "--headless=new",
  "--disable-gpu-sandbox",
  "--no-first-run",
  "--no-default-browser-check",
  "--allow-file-access-from-files",
  "--remote-debugging-port=0",
  `--user-data-dir=${profile}`,
  pathToFileURL(join(output, "index.html")).href
], { stdio: "ignore", windowsHide: true });

try {
  const portFile = join(profile, "DevToolsActivePort");
  let port;
  for (let attempt = 0; attempt < 100; attempt += 1) {
    try {
      port = (await readFile(portFile, "utf8")).split(/\r?\n/u)[0];
      break;
    } catch {
      await new Promise(resolveDelay => setTimeout(resolveDelay, 100));
    }
  }
  if (!port) throw new Error("Chrome did not publish DevToolsActivePort");
  const targets = await fetch(`http://127.0.0.1:${port}/json/list`).then(response => response.json());
  const page = targets.find(target => target.type === "page");
  if (!page) throw new Error("Rendering spike page target not found");
  const socket = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise((resolveOpen, rejectOpen) => {
    socket.addEventListener("open", resolveOpen, { once: true });
    socket.addEventListener("error", rejectOpen, { once: true });
  });
  let id = 0;
  const pending = new Map();
  socket.addEventListener("message", event => {
    const message = JSON.parse(event.data);
    if (!message.id || !pending.has(message.id)) return;
    const { resolve: resolveMessage, reject } = pending.get(message.id);
    pending.delete(message.id);
    if (message.error) reject(new Error(message.error.message)); else resolveMessage(message.result);
  });
  const send = (method, params = {}) => new Promise((resolveMessage, reject) => {
    const requestId = ++id;
    pending.set(requestId, { resolve: resolveMessage, reject });
    socket.send(JSON.stringify({ id: requestId, method, params }));
  });
  await send("Page.enable");
  const ready = await send("Runtime.evaluate", {
    expression: "document.fonts.ready.then(() => window.collectSpikeMetrics())",
    awaitPromise: true,
    returnByValue: true
  });
  const metrics = ready.result.value;
  for (const [scale, width, height] of [[1, 1440, 900], [2, 1440, 900]]) {
    await send("Emulation.setDeviceMetricsOverride", { width, height, deviceScaleFactor: scale, mobile: false });
    const screenshot = await send("Page.captureScreenshot", { format: "png", fromSurface: true, captureBeyondViewport: true });
    await writeFile(join(output, `rendering-spike-${scale}x.png`), Buffer.from(screenshot.data, "base64"));
  }
  await writeFile(join(output, "metrics.json"), `${JSON.stringify(metrics, null, 2)}\n`);
  socket.close();
  if (!Object.values(metrics.fonts).every(Boolean)) throw new Error("At least one embedded font failed to load");
  if (!Object.values(metrics.modes).every(mode => mode.textPreserved)) throw new Error("A renderer changed Unicode text");
  if (!metrics.surroundingTypographyPreserved) throw new Error("Surrounding typography changed");
  process.stdout.write(`Rendering spike passed\n${output}\n`);
} finally {
  browser.kill();
  if (browser.exitCode === null) await new Promise(resolveExit => browser.once("exit", resolveExit));
  await rm(profile, { recursive: true, force: true });
}
