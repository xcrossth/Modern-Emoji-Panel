import { execFileSync } from "node:child_process";
import { createHash } from "node:crypto";
import { cp, mkdir, readFile, rm, writeFile } from "node:fs/promises";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { createDeterministicZip, listFiles } from "./deterministic-zip.mjs";

const extensionRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const repositoryRoot = resolve(extensionRoot, "..", "..");
const artifactRoot = resolve(repositoryRoot, "artifacts", "renderer-extension");
const productionRoot = join(artifactRoot, "production");
const releaseRoot = join(artifactRoot, "release");
const packageRoot = join(releaseRoot, "package");

function assertChildPath(parent, child, label) {
  const childRelative = relative(parent, child);
  if (childRelative.startsWith("..") || childRelative === "") {
    throw new Error(`Unexpected ${label} path: ${child}`);
  }
}

function sha256(data) {
  return createHash("sha256").update(data).digest("hex");
}

async function readJson(path) {
  return JSON.parse(await readFile(path, "utf8"));
}

function git(...args) {
  return execFileSync("git", args, { cwd: repositoryRoot, encoding: "utf8" }).trim();
}

const manifest = await readJson(join(extensionRoot, "manifest.json"));
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
const extensionFontEvidencePath = join(
  repositoryRoot,
  "docs",
  "qualification",
  "results",
  "renderer-font-runtime-win10-20260830.json",
);
const extensionFontEvidence = await readJson(extensionFontEvidencePath);
const manualEvidencePath = join(
  repositoryRoot,
  "docs",
  "qualification",
  "results",
  "renderer-manual-primary-sites-win10-20260830.json",
);
const manualEvidence = await readJson(manualEvidencePath);
const instagramImageEvidencePath = join(
  repositoryRoot,
  "docs",
  "qualification",
  "results",
  "renderer-instagram-emoji-images-win10-20260830.json",
);
const instagramImageEvidence = await readJson(instagramImageEvidencePath);
const facebookMessengerEvidencePath = join(
  repositoryRoot,
  "docs",
  "qualification",
  "results",
  "renderer-facebook-messenger-image-emoji-win10-20260830.json",
);
const facebookMessengerEvidence = await readJson(facebookMessengerEvidencePath);
const sourceCommit = git("rev-parse", "HEAD");
const sourceTreeClean = git("status", "--porcelain").length === 0;
const sourceDateEpoch = Number(process.env.SOURCE_DATE_EPOCH ?? git("log", "-1", "--format=%ct"));
if (!Number.isSafeInteger(sourceDateEpoch) || sourceDateEpoch <= 0) {
  throw new Error("SOURCE_DATE_EPOCH must be a positive integer.");
}
const deterministicDate = new Date(sourceDateEpoch * 1000);

assertChildPath(artifactRoot, productionRoot, "production");
assertChildPath(artifactRoot, releaseRoot, "release");
assertChildPath(releaseRoot, packageRoot, "package");

execFileSync(
  process.execPath,
  [join(extensionRoot, "scripts", "build.mjs"), "--production"],
  { cwd: extensionRoot, stdio: "inherit" },
);

await rm(releaseRoot, { recursive: true, force: true });
await mkdir(packageRoot, { recursive: true });
await cp(productionRoot, packageRoot, { recursive: true });
await cp(join(extensionRoot, "README.md"), join(packageRoot, "README.md"));
await cp(join(repositoryRoot, "LICENSE"), join(packageRoot, "LICENSE"));
await cp(join(extensionRoot, "THIRD-PARTY-NOTICES.md"), join(packageRoot, "THIRD-PARTY-NOTICES.md"));
await mkdir(join(packageRoot, "licenses"), { recursive: true });
await cp(
  join(repositoryRoot, "vendor", "emoji-baseline", "licenses", "UNICODE-LICENSE-V3.txt"),
  join(packageRoot, "licenses", "UNICODE-LICENSE-V3.txt"),
);

const fontPath = join(packageRoot, "assets", "fonts", "Noto-COLRv1.ttf");
const fontHash = sha256(await readFile(fontPath));
const qualificationHash = sha256(await readFile(qualificationPath));
const allPrimarySitesPassed = qualification.manual?.status === "passed"
  && facebookMessengerEvidence.status === "passed"
  && Object.values(facebookMessengerEvidence.sites ?? {}).every(site => site.status === "passed");
const metadata = {
  schemaVersion: 1,
  product: manifest.name,
  extensionVersion: manifest.version,
  manifestVersion: manifest.manifest_version,
  releaseKind: allPrimarySitesPassed ? "release" : "release-candidate",
  unicodeVersion: baseline.baseline.unicode,
  unicodeEmojiVersion: baseline.baseline.emoji,
  cldrVersion: baseline.baseline.cldr,
  baselineId: baseline.baselineId,
  notoEmojiVersion: baseline.baseline.notoEmoji,
  notoEmojiCommit: baseline.baseline.notoCommit,
  renderer: "noto-colrv1",
  fontSha256: fontHash,
  sourceCommit,
  sourceTreeClean,
  sourceDateEpoch,
  sourceDateUtc: deterministicDate.toISOString(),
  qualification: {
    status: qualification.status,
    report: "docs/qualification/results/renderer-automated-win10-20260830/qualification-report.json",
    reportSha256: qualificationHash,
    manualMatrix: qualification.manual?.matrix ?? null,
    manualEvidence: {
      status: manualEvidence.status,
      report: "docs/qualification/results/renderer-manual-primary-sites-win10-20260830.json",
      reportSha256: sha256(await readFile(manualEvidencePath)),
    },
    instagramImageEmoji: {
      status: instagramImageEvidence.status,
      report: "docs/qualification/results/renderer-instagram-emoji-images-win10-20260830.json",
      reportSha256: sha256(await readFile(instagramImageEvidencePath)),
    },
    facebookMessengerImageEmoji: {
      status: facebookMessengerEvidence.status,
      report: "docs/qualification/results/renderer-facebook-messenger-image-emoji-win10-20260830.json",
      reportSha256: sha256(await readFile(facebookMessengerEvidencePath)),
    },
    extensionFont: {
      status: extensionFontEvidence.status,
      report: "docs/qualification/results/renderer-font-runtime-win10-20260830.json",
      reportSha256: sha256(await readFile(extensionFontEvidencePath)),
      familyName: extensionFontEvidence.afterFix?.actualGlyphFont?.familyName ?? null,
      isCustomFont: extensionFontEvidence.afterFix?.actualGlyphFont?.isCustomFont ?? null,
      requestScheme: extensionFontEvidence.afterFix?.fontRequestScheme ?? null,
    },
  },
  defaults: {
    debug: false,
    mode: "allowlist",
    primarySites: ["instagram.com", "tiktok.com", "facebook.com", "messenger.com"],
  },
};
await writeFile(join(packageRoot, "release-metadata.json"), `${JSON.stringify(metadata, null, 2)}\n`, "utf8");

const filesBeforeChecksums = await listFiles(packageRoot);
const checksumLines = [];
for (const fileName of filesBeforeChecksums) {
  const data = await readFile(join(packageRoot, ...fileName.split("/")));
  checksumLines.push(`${sha256(data)} *${fileName}`);
}
await writeFile(join(packageRoot, "SHA256SUMS.txt"), `${checksumLines.join("\n")}\n`, "utf8");

const packageFiles = await listFiles(packageRoot);
const zipName = `modern-emoji-renderer-${manifest.version}.zip`;
const zipPath = join(releaseRoot, zipName);
const zip = await createDeterministicZip(packageRoot, packageFiles, deterministicDate);
await writeFile(zipPath, zip);
const zipHash = sha256(zip);
await writeFile(join(releaseRoot, `${zipName}.sha256`), `${zipHash} *${zipName}\n`, "utf8");

console.log(JSON.stringify({
  zipPath,
  sha256: zipHash,
  files: packageFiles.length,
  sourceTreeClean,
  releaseKind: metadata.releaseKind,
  qualificationStatus: qualification.status,
  facebookMessengerQualificationStatus: facebookMessengerEvidence.status,
}, null, 2));
