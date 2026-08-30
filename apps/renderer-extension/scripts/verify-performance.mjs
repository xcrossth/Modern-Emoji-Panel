import { mkdir, mkdtemp, readFile, readdir, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { spawn } from "node:child_process";

const root = resolve(import.meta.dirname, "../../..");
const chromeRoot = join(root, "artifacts", "tooling", "chrome-for-testing");
const fixture = join(root, "artifacts", "renderer-extension", "unpacked", "fixtures", "performance.html");
const evidence = join(root, "artifacts", "renderer-extension", "evidence", "ticket-09");
const versions = (await readdir(chromeRoot, { withFileTypes: true }))
  .filter(entry => entry.isDirectory()).map(entry => entry.name)
  .sort((left, right) => right.localeCompare(left, undefined, { numeric: true }));
if (versions.length === 0) throw new Error("Chrome for Testing is not installed");
const chrome = join(chromeRoot, versions[0], "chrome-win64", "chrome.exe");
const profile = await mkdtemp(join(tmpdir(), "modern-emoji-performance-"));
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
  if (!page) throw new Error("Performance fixture page target not found");
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
  await send("Performance.enable");
  await send("HeapProfiler.enable");
  await send("HeapProfiler.collectGarbage");
  const heapMetric = metrics => metrics.metrics.find(metric => metric.name === "JSHeapUsedSize")?.value ?? 0;
  const heapBefore = heapMetric(await send("Performance.getMetrics"));
  const evaluated = await send("Runtime.evaluate", {
    expression: `(async () => {
      for (let attempt = 0; attempt < 100; attempt += 1) {
        if (typeof window.runPerformanceFixture === 'function') return window.runPerformanceFixture();
        await new Promise(resolve => setTimeout(resolve, 25));
      }
      throw new Error('Performance fixture did not initialize');
    })()`,
    awaitPromise: true,
    returnByValue: true,
  });
  if (evaluated.exceptionDetails) {
    throw new Error(evaluated.exceptionDetails.exception?.description ?? evaluated.exceptionDetails.text);
  }
  await send("HeapProfiler.collectGarbage");
  const heapAfter = heapMetric(await send("Performance.getMetrics"));
  const report = { chromeVersion: versions[0], heapBefore, heapAfter, retainedHeapBytes: heapAfter - heapBefore, ...evaluated.result.value };
  const { budgets } = report;
  const assertions = {
    initialTime: report.initial.milliseconds <= budgets.initialMilliseconds,
    initialWrappers: report.initial.wrappers === budgets.initialMessages * 2,
    burstTime: report.burst.milliseconds <= budgets.burstMilliseconds,
    burstWrappers: report.burst.wrappers === budgets.burstMessages * 2,
    maxBatch: Math.max(
      report.initial.metrics.maxBatchMilliseconds,
      report.burst.metrics.maxBatchMilliseconds,
      report.navigation.metrics.maxBatchMilliseconds,
    ) <= budgets.maxBatchMilliseconds,
    navigationTime: report.navigation.metrics.processingMilliseconds <= budgets.navigationProcessingMilliseconds,
    navigationWrappers: report.navigation.currentWrappers === budgets.messagesPerNavigation * 2,
    noScrollGrowth: report.navigation.wrappersBeforeScroll === report.navigation.wrappersAfterScroll,
    noObserverGrowth: report.navigation.metrics.wrappersCreated ===
      budgets.navigationCycles * budgets.messagesPerNavigation * 2,
    retainedHeap: report.retainedHeapBytes <= budgets.retainedHeapBytes,
    editablePreserved: report.editablePreserved,
  };
  report.assertions = assertions;
  await writeFile(join(evidence, "performance-report.json"), `${JSON.stringify(report, null, 2)}\n`);
  for (const [name, passed] of Object.entries(assertions)) {
    if (!passed) throw new Error(`Performance qualification failed: ${name}`);
  }
  socket.close();
  process.stdout.write(`Renderer performance fixture passed\n${evidence}\n`);
} finally {
  browser.kill();
  if (browser.exitCode === null) await new Promise(done => browser.once("exit", done));
  await rm(profile, { recursive: true, force: true });
}
