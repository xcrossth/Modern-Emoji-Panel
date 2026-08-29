import { createHash } from "node:crypto";
import { readFileSync } from "node:fs";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { listFiles } from "./deterministic-zip.mjs";

const extensionRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const repositoryRoot = resolve(extensionRoot, "..", "..");
const releaseRoot = join(repositoryRoot, "artifacts", "renderer-extension", "release");
const packageRoot = join(releaseRoot, "package");

function sha256(data) {
  return createHash("sha256").update(data).digest("hex");
}

async function readJson(path) {
  return JSON.parse(await readFile(path, "utf8"));
}

function check(name, passed, detail) {
  return { name, passed: Boolean(passed), detail };
}

function readStoredZip(zip) {
  const entries = new Map();
  let offset = 0;
  while (offset + 4 <= zip.length && zip.readUInt32LE(offset) === 0x04034b50) {
    const method = zip.readUInt16LE(offset + 8);
    const compressedSize = zip.readUInt32LE(offset + 18);
    const uncompressedSize = zip.readUInt32LE(offset + 22);
    const nameLength = zip.readUInt16LE(offset + 26);
    const extraLength = zip.readUInt16LE(offset + 28);
    if (method !== 0 || compressedSize !== uncompressedSize) {
      throw new Error("Release ZIP must use deterministic STORE entries.");
    }
    const nameStart = offset + 30;
    const dataStart = nameStart + nameLength + extraLength;
    const name = zip.subarray(nameStart, nameStart + nameLength).toString("utf8");
    const data = zip.subarray(dataStart, dataStart + uncompressedSize);
    entries.set(name, Buffer.from(data));
    offset = dataStart + compressedSize;
  }
  return entries;
}

const manifest = await readJson(join(packageRoot, "manifest.json"));
const metadata = await readJson(join(packageRoot, "release-metadata.json"));
const baseline = await readJson(join(repositoryRoot, "data", "emoji-baseline", "17.0", "source-manifest.json"));
const qualificationPath = join(
  repositoryRoot,
  "docs",
  "qualification",
  "results",
  "renderer-automated-win10-20260830",
  "qualification-report.json",
);
const qualification = await readJson(qualificationPath);
const packageFiles = await listFiles(packageRoot);
const zipName = `modern-emoji-renderer-${manifest.version}.zip`;
const zipPath = join(releaseRoot, zipName);
const zip = await readFile(zipPath);
const zipEntries = readStoredZip(zip);

const requiredFiles = [
  "LICENSE",
  "README.md",
  "SHA256SUMS.txt",
  "THIRD-PARTY-NOTICES.md",
  "assets/fonts/Noto-COLRv1.ttf",
  "assets/fonts/OFL.txt",
  "assets/styles/renderer.css",
  "background/service-worker.js",
  "content/index.js",
  "licenses/UNICODE-LICENSE-V3.txt",
  "manifest.json",
  "options/options.css",
  "options/options.html",
  "options/options.js",
  "popup/popup.css",
  "popup/popup.html",
  "popup/popup.js",
  "release-metadata.json",
];
const forbiddenPaths = packageFiles.filter(file => (
  /(^|\/)(fixtures?|tests?|src|data)(\/|$)/iu.test(file)
  || /\.map$/iu.test(file)
  || /(^|\/)(\.env|secrets?)(\.|\/|$)/iu.test(file)
));
const missingRequired = requiredFiles.filter(file => !packageFiles.includes(file));
const unexpectedZipEntries = [...zipEntries.keys()].filter(file => !packageFiles.includes(file));
const missingZipEntries = packageFiles.filter(file => !zipEntries.has(file));
const changedZipEntries = packageFiles.filter(file => {
  const zipped = zipEntries.get(file);
  return !zipped || !zipped.equals(readFileSync(join(packageRoot, ...file.split("/"))));
});

const checksumText = await readFile(join(packageRoot, "SHA256SUMS.txt"), "utf8");
const checksumLines = checksumText.trim().split(/\r?\n/u);
const checksumMap = new Map(checksumLines.map(line => {
  const match = /^([0-9a-f]{64}) \*(.+)$/u.exec(line);
  return match ? [match[2], match[1]] : [line, "invalid"];
}));
const checksumTargets = packageFiles.filter(file => file !== "SHA256SUMS.txt");
const badChecksums = [];
for (const file of checksumTargets) {
  const actual = sha256(await readFile(join(packageRoot, ...file.split("/"))));
  if (checksumMap.get(file) !== actual) badChecksums.push(file);
}

const executableFiles = packageFiles.filter(file => /\.(?:js|html|css)$/iu.test(file));
const remoteCodeHits = [];
const prohibitedRuntimePatterns = [
  /\bfetch\s*\(/u,
  /\bXMLHttpRequest\b/u,
  /\bWebSocket\b/u,
  /\bEventSource\b/u,
  /\beval\s*\(/u,
  /\bnew\s+Function\b/u,
  /<script[^>]+src=["']https?:\/\//iu,
  /\bimport\s*\(\s*["']https?:\/\//u,
];
for (const file of executableFiles) {
  const text = await readFile(join(packageRoot, ...file.split("/")), "utf8");
  if (prohibitedRuntimePatterns.some(pattern => pattern.test(text))) remoteCodeHits.push(file);
}

const font = await readFile(join(packageRoot, "assets", "fonts", "Noto-COLRv1.ttf"));
const sidecar = (await readFile(join(releaseRoot, `${zipName}.sha256`), "utf8")).trim();
const expectedPermissions = ["storage", "activeTab", "scripting"];
const expectedHosts = ["https://www.instagram.com/*", "https://www.tiktok.com/*"];
const checks = [
  check("มีไฟล์ production ที่จำเป็นครบ", missingRequired.length === 0, missingRequired),
  check("ไม่มี fixture, test, source map, data dump หรือ secret", forbiddenPaths.length === 0, forbiddenPaths),
  check("ZIP มีไฟล์ตรงกับ staging ทุกไบต์", unexpectedZipEntries.length === 0 && missingZipEntries.length === 0 && changedZipEntries.length === 0, { unexpectedZipEntries, missingZipEntries, changedZipEntries }),
  check("SHA256SUMS ครบและถูกต้อง", checksumMap.size === checksumTargets.length && badChecksums.length === 0, badChecksums),
  check("SHA-256 ของ ZIP ตรงกับ sidecar", sidecar === `${sha256(zip)} *${zipName}`, sidecar),
  check("Manifest V3 และ version metadata ตรงกัน", manifest.manifest_version === 3 && metadata.extensionVersion === manifest.version && metadata.manifestVersion === 3, { manifest: manifest.version, metadata: metadata.extensionVersion }),
  check("Unicode/Noto metadata ตรง baseline", metadata.unicodeVersion === baseline.baseline.unicode && metadata.unicodeEmojiVersion === baseline.baseline.emoji && metadata.notoEmojiVersion === baseline.baseline.notoEmoji && metadata.baselineId === baseline.baselineId, metadata.baselineId),
  check("Font hash ตรง asset", metadata.fontSha256 === sha256(font), metadata.fontSha256),
  check("สิทธิ์หลักมีเท่าที่กำหนด", JSON.stringify(manifest.permissions) === JSON.stringify(expectedPermissions) && JSON.stringify(manifest.host_permissions) === JSON.stringify(expectedHosts) && JSON.stringify(manifest.optional_host_permissions) === JSON.stringify(["<all_urls>"]), { permissions: manifest.permissions, hostPermissions: manifest.host_permissions, optionalHostPermissions: manifest.optional_host_permissions }),
  check("Debug ปิดเป็นค่าเริ่มต้น", metadata.defaults?.debug === false, metadata.defaults),
  check("มี source, Noto และ Unicode licenses", ["LICENSE", "assets/fonts/OFL.txt", "licenses/UNICODE-LICENSE-V3.txt", "THIRD-PARTY-NOTICES.md"].every(file => packageFiles.includes(file)), null),
  check("ไม่มี Apple Emoji ในแพ็กเกจ", !packageFiles.some(file => /apple/iu.test(file)), packageFiles.filter(file => /apple/iu.test(file))),
  check("ไม่มี runtime network API หรือ remote code", remoteCodeHits.length === 0, remoteCodeHits),
  check("อ้างอิง qualification report ตรงไฟล์จริง", metadata.qualification?.status === qualification.status && metadata.qualification?.reportSha256 === sha256(await readFile(qualificationPath)), metadata.qualification),
];

const passed = checks.every(item => item.passed);
const report = {
  schemaVersion: 1,
  status: passed ? "passed" : "failed",
  releaseKind: metadata.releaseKind,
  qualificationStatus: qualification.status,
  manualGate: qualification.manual,
  zip: { name: zipName, sha256: sha256(zip), bytes: zip.length },
  packageFiles,
  checks,
};
await mkdir(releaseRoot, { recursive: true });
await writeFile(join(releaseRoot, "verification-report.json"), `${JSON.stringify(report, null, 2)}\n`, "utf8");
const markdown = [
  "# รายงานตรวจ Renderer Extension Release",
  "",
  `สถานะ: **${passed ? "ผ่าน" : "ไม่ผ่าน"}**`,
  "",
  `ชนิดแพ็กเกจ: **${metadata.releaseKind}**`,
  "",
  `Qualification: **${qualification.status}**${qualification.manual?.status === "pending" ? " — ยังรอ manual E2E บนบัญชีจริง" : ""}`,
  "",
  `ZIP: \`${zipName}\``,
  "",
  `SHA-256: \`${sha256(zip)}\``,
  "",
  "## ผลตรวจ",
  "",
  ...checks.map(item => `- ${item.passed ? "ผ่าน" : "ไม่ผ่าน"}: ${item.name}`),
  "",
].join("\n");
await writeFile(join(releaseRoot, "verification-report.md"), markdown, "utf8");

console.log(`Renderer release verification: ${passed ? "PASSED" : "FAILED"}`);
console.log(`Report: ${join(releaseRoot, "verification-report.md")}`);
if (!passed) process.exitCode = 1;
