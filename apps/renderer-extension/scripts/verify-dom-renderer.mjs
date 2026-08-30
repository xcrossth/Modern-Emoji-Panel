import { mkdir, mkdtemp, readFile, readdir, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { spawn } from "node:child_process";

const root = resolve(import.meta.dirname, "../../..");
const chromeRoot = join(root, "artifacts", "tooling", "chrome-for-testing");
const fixture = join(root, "artifacts", "renderer-extension", "unpacked", "fixtures", "dom-renderer.html");
const evidence = join(root, "artifacts", "renderer-extension", "evidence", "tickets-03-04");
const versions = (await readdir(chromeRoot, { withFileTypes: true }))
  .filter(entry => entry.isDirectory()).map(entry => entry.name)
  .sort((left, right) => right.localeCompare(left, undefined, { numeric: true }));
if (versions.length === 0) throw new Error("Chrome for Testing is not installed");
const chrome = join(chromeRoot, versions[0], "chrome-win64", "chrome.exe");
const profile = await mkdtemp(join(tmpdir(), "modern-emoji-dom-fixture-"));
await rm(evidence, { recursive: true, force: true });
await mkdir(evidence, { recursive: true });
const browser = spawn(chrome, [
  "--headless=new", "--no-first-run", "--no-default-browser-check", "--allow-file-access-from-files",
  "--remote-debugging-port=0", `--user-data-dir=${profile}`, pathToFileURL(fixture).href
], { stdio: "ignore", windowsHide: true });

try {
  let port;
  for (let attempt = 0; attempt < 100; attempt += 1) {
    try { port = (await readFile(join(profile, "DevToolsActivePort"), "utf8")).split(/\r?\n/u)[0]; break; }
    catch { await new Promise(done => setTimeout(done, 100)); }
  }
  if (!port) throw new Error("Chrome did not publish DevToolsActivePort");
  const targets = await fetch(`http://127.0.0.1:${port}/json/list`).then(response => response.json());
  const page = targets.find(target => target.type === "page");
  if (!page) throw new Error("DOM fixture page target not found");
  const socket = new WebSocket(page.webSocketDebuggerUrl);
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
  await send("Page.enable");
  const evaluated = await send("Runtime.evaluate", {
    expression: `(async () => {
      for (let attempt = 0; attempt < 100; attempt += 1) {
        if (typeof window.collectDomFixtureReport === "function") return window.collectDomFixtureReport();
        await new Promise(resolve => setTimeout(resolve, 25));
      }
      throw new Error("DOM fixture module did not initialize");
    })()`,
    awaitPromise: true,
    returnByValue: true
  });
  if (evaluated.exceptionDetails) {
    throw new Error(evaluated.exceptionDetails.exception?.description ?? evaluated.exceptionDetails.text);
  }
  const report = evaluated.result.value;
  const copyEvaluation = await send("Runtime.evaluate", {
    expression: `(() => {
      const source = document.querySelector("#initial");
      const expected = source.textContent;
      const range = document.createRange();
      range.selectNodeContents(source);
      const selection = window.getSelection();
      selection.removeAllRanges();
      selection.addRange(range);
      let copied = "";
      document.addEventListener("copy", event => {
        copied = selection.toString();
        event.preventDefault();
      }, { once: true });
      const accepted = document.execCommand("copy");
      return { accepted, copyPreserved: copied === expected };
    })()`,
    userGesture: true,
    returnByValue: true
  });
  report.copyCommandAccepted = copyEvaluation.result.value.accepted;
  report.copyPreserved = copyEvaluation.result.value.copyPreserved;
  const capture = await send("Page.captureScreenshot", { format: "png", fromSurface: true });
  await writeFile(join(evidence, "dom-renderer.png"), Buffer.from(capture.data, "base64"));
  await writeFile(join(evidence, "report.json"), `${JSON.stringify(report, null, 2)}\n`);
  socket.close();
  for (const key of [
    "fontLoaded", "initialTextPreserved", "selectionPreserved", "copyCommandAccepted", "copyPreserved", "browserFindSucceeded",
    "editablePreserved", "codePreserved", "dynamicTextPreserved"
  ]) {
    if (!report[key]) throw new Error(`DOM fixture assertion failed: ${key}`);
  }
  if (report.wrapperCount !== 612) throw new Error(`Expected 612 wrappers, got ${report.wrapperCount}`);
  if (report.metrics.batches < 12) throw new Error("Long transcript was not split into idle-sized batches");
  process.stdout.write(`DOM renderer fixture passed\n${evidence}\n`);
} finally {
  browser.kill();
  if (browser.exitCode === null) await new Promise(done => browser.once("exit", done));
  await rm(profile, { recursive: true, force: true });
}
