import { cp, mkdir, readFile, rm } from "node:fs/promises";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { build } from "esbuild";

const extensionRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const repositoryRoot = resolve(extensionRoot, "..", "..");
const artifactRoot = resolve(repositoryRoot, "artifacts", "renderer-extension");
const production = process.argv.includes("--production");
const outputRoot = resolve(artifactRoot, production ? "production" : "unpacked");
const relativeOutput = relative(artifactRoot, outputRoot);

if (relativeOutput.startsWith("..") || relativeOutput === "") {
  throw new Error(`Refusing to clean unexpected output path: ${outputRoot}`);
}

await rm(outputRoot, { recursive: true, force: true });
await mkdir(outputRoot, { recursive: true });

const productionEntryPoints = {
  "background/service-worker": join(extensionRoot, "src", "background", "service-worker.ts"),
  "content/index": join(extensionRoot, "src", "content", "index.ts"),
  "popup/popup": join(extensionRoot, "src", "popup", "popup.ts"),
  "options/options": join(extensionRoot, "src", "options", "options.ts"),
};
const developmentEntryPoints = {
  ...productionEntryPoints,
  "fixtures/dom-renderer": join(extensionRoot, "src", "fixtures", "dom-renderer.ts"),
  "fixtures/performance": join(extensionRoot, "src", "fixtures", "performance.ts"),
};

await build({
  entryPoints: production ? productionEntryPoints : developmentEntryPoints,
  outdir: outputRoot,
  bundle: true,
  format: "esm",
  platform: "browser",
  target: "chrome120",
  sourcemap: false,
  minify: production,
  legalComments: "none",
  logLevel: "info",
});

const manifestSource = join(extensionRoot, "manifest.json");
const manifest = JSON.parse(await readFile(manifestSource, "utf8"));
if (manifest.manifest_version !== 3) {
  throw new Error("Renderer Extension must use Manifest V3.");
}

await cp(manifestSource, join(outputRoot, "manifest.json"));
await cp(join(extensionRoot, "assets"), join(outputRoot, "assets"), { recursive: true });
await cp(join(extensionRoot, "ui", "popup", "popup.html"), join(outputRoot, "popup", "popup.html"));
await cp(join(extensionRoot, "ui", "popup", "popup.css"), join(outputRoot, "popup", "popup.css"));
await cp(join(extensionRoot, "ui", "options", "options.html"), join(outputRoot, "options", "options.html"));
await cp(join(extensionRoot, "ui", "options", "options.css"), join(outputRoot, "options", "options.css"));
if (!production) {
  await mkdir(join(outputRoot, "data"), { recursive: true });
  await cp(
    join(extensionRoot, "src", "generated", "emoji-sequences.json"),
    join(outputRoot, "data", "emoji-sequences.json"),
  );
  await mkdir(join(outputRoot, "fixtures"), { recursive: true });
  await cp(
    join(extensionRoot, "tests", "fixtures", "rendering.html"),
    join(outputRoot, "fixtures", "rendering.html"),
  );
  await cp(
    join(extensionRoot, "tests", "fixtures", "dom-renderer.html"),
    join(outputRoot, "fixtures", "dom-renderer.html"),
  );
  await cp(
    join(extensionRoot, "tests", "fixtures", "performance.html"),
    join(outputRoot, "fixtures", "performance.html"),
  );
}
console.log(`Renderer Extension ${production ? "production" : "development"} build: ${outputRoot}`);
