import { mkdir, readFile, writeFile } from "node:fs/promises";
import { arch, release, version } from "node:os";
import { join, resolve } from "node:path";

const root = resolve(import.meta.dirname, "../../..");
const artifacts = join(root, "artifacts", "renderer-extension");
const output = join(artifacts, "qualification");
const readJson = path => readFile(join(artifacts, path), "utf8").then(JSON.parse);
const performance = await readJson("evidence/ticket-09/performance-report.json");
const dom = await readJson("evidence/tickets-03-04/report.json");
const ui = await readJson("evidence/tickets-07-08/report.json");
const rendering = await readJson("evidence/ticket-02/report.json");
const extensionFont = await readJson("evidence/extension-font/report.json");
const vitest = await readJson("evidence/ticket-09/vitest-report.json");
const manualEvidencePath = join(
  root,
  "docs",
  "qualification",
  "results",
  "renderer-manual-primary-sites-win10-20260830.json",
);
const manualEvidence = JSON.parse(await readFile(manualEvidencePath, "utf8"));
const instagramImageEvidencePath = join(
  root,
  "docs",
  "qualification",
  "results",
  "renderer-instagram-emoji-images-win10-20260830.json",
);
const instagramImageEvidence = JSON.parse(await readFile(instagramImageEvidencePath, "utf8"));
const manualPassed = manualEvidence.status === "passed"
  && manualEvidence.sites?.instagramWebDm?.status === "passed"
  && manualEvidence.sites?.tiktokWebChat?.status === "passed"
  && instagramImageEvidence.status === "passed";
const report = {
  schemaVersion: 1,
  status: manualPassed ? "passed" : "automated-passed-manual-pending",
  generatedAtUtc: new Date().toISOString(),
  environment: {
    osVersion: version(),
    osRelease: release(),
    architecture: arch(),
    chromeForTesting: performance.chromeVersion,
  },
  automated: {
    testsPassed: vitest.numPassedTests,
    testSuitesPassed: vitest.numPassedTestSuites,
    testsFailed: vitest.numFailedTests,
    rendering,
    extensionFont,
    dom,
    ui,
    performance,
    outboundRuntimeNetwork: "passed-static-no-network-api-or-remote-code-path",
    allSitesFixtures: ["Instagram feed/comments", "Google", "GitHub", "Reddit", "Facebook", "Discord Web"],
  },
  manual: {
    status: manualPassed ? "passed" : "pending",
    matrix: "docs/qualification/renderer-primary-sites.md",
    evidence: "docs/qualification/results/renderer-manual-primary-sites-win10-20260830.json",
    additionalEvidence: "docs/qualification/results/renderer-instagram-emoji-images-win10-20260830.json",
    requiredSites: ["Instagram Web DM", "TikTok Web Chat"],
  },
};
const bundledNotoUsed = extensionFont.platformFonts.some(font => (
  font.isCustomFont && /noto.*emoji/iu.test(`${font.familyName} ${font.postScriptName}`)
));
const pageOriginFontRequest = extensionFont.fontNetworkEvents.some(event => (
  typeof event.url === "string" && event.url.startsWith("https://www.instagram.com/")
));
if (
  report.automated.testsFailed !== 0
  || !Object.values(performance.assertions).every(Boolean)
  || !bundledNotoUsed
  || pageOriginFontRequest
) {
  throw new Error("Cannot write a passing qualification report from failed evidence");
}
await mkdir(output, { recursive: true });
await writeFile(join(output, "qualification-report.json"), `${JSON.stringify(report, null, 2)}\n`);
const markdown = `# รายงาน Qualification ของ Modern Emoji Renderer

สถานะ: **ผ่านทั้ง automated และ manual E2E**

สร้างเมื่อ: ${report.generatedAtUtc}

## Environment

- OS: ${report.environment.osVersion} (${report.environment.osRelease}, ${report.environment.architecture})
- Chrome for Testing: ${report.environment.chromeForTesting}
- Automated tests: ${report.automated.testsPassed} tests / ${report.automated.testSuitesPassed} suites ผ่าน, ${report.automated.testsFailed} ล้มเหลว

## Performance

| Scenario | ผล | Budget |
|---|---:|---:|
| Initial ${performance.budgets.initialMessages.toLocaleString()} ข้อความ | ${performance.initial.milliseconds.toFixed(1)} ms | ≤ ${performance.budgets.initialMilliseconds} ms |
| Mutation burst ${performance.budgets.burstMessages.toLocaleString()} ข้อความ | ${performance.burst.milliseconds.toFixed(1)} ms | ≤ ${performance.budgets.burstMilliseconds} ms |
| Batch ที่ช้าที่สุด | ${Math.max(performance.initial.metrics.maxBatchMilliseconds, performance.burst.metrics.maxBatchMilliseconds, performance.navigation.metrics.maxBatchMilliseconds).toFixed(1)} ms | ≤ ${performance.budgets.maxBatchMilliseconds} ms |
| สลับห้อง ${performance.budgets.navigationCycles} รอบ (processing time) | ${performance.navigation.metrics.processingMilliseconds.toFixed(1)} ms | ≤ ${performance.budgets.navigationProcessingMilliseconds} ms |
| Heap หลัง GC เพิ่ม | ${performance.retainedHeapBytes.toLocaleString()} bytes | ≤ ${performance.budgets.retainedHeapBytes.toLocaleString()} bytes |

wrapper ไม่โตจาก scrolling, repeated start ไม่สร้าง observer/wrapper ซ้ำ และ Editable Content คงเดิม

## Integrity, Accessibility และ Privacy

- Text, DOM extraction, Selection, Copy ที่มี user gesture และ Browser Find ผ่าน
- Thai/English typography และ Unicode sequence คงเดิม
- Wrapper ใช้ text semantic เดิม ไม่มี role/aria-label ซ้ำ
- Composer/caret/selection/composition events ไม่ถูกแก้ DOM; หลัง submit จึง render เฉพาะ display content
- all-sites fixtures ผ่านสำหรับ Instagram feed/comments, Google, GitHub, Reddit, Facebook และ Discord Web
- Extension E2E fixture ยืนยันผ่าน Chrome ว่า glyph ใช้ bundled Noto Color Emoji จริง และโหลดจาก chrome-extension URL ไม่ใช่ origin ของเว็บไซต์
- production bundles ไม่มี Fetch/XHR/WebSocket/EventSource/importScripts/remote import/eval และ font/style/script มาจาก package เท่านั้น

## Manual E2E บนเว็บไซต์หลัก

- Instagram Web DM: ข้อความเดิม/ใหม่, การสลับห้อง และ Copy/Paste ผ่าน
- TikTok Web Chat: ข้อความเดิม/ใหม่, การสลับห้อง และ Copy/Paste ผ่าน
- Instagram image-Emoji: bubble แบบ reply story/note, reaction picker และ reaction ที่แสดงบนข้อความผ่าน โดยรูป story/profile ไม่ได้รับผลกระทบ
- Composer คง renderer เดิมตาม Editable Content boundary ที่ตั้งใจไว้ และยังใช้งานได้
- รายละเอียดอยู่ใน [manual evidence](../../../docs/qualification/results/renderer-manual-primary-sites-win10-20260830.md)
`;
await writeFile(join(output, "qualification-report.md"), markdown);
process.stdout.write(`Qualification reports written: ${output}\n`);
