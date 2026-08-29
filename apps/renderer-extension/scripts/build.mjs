import { cp, mkdir, readFile, rm } from "node:fs/promises";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { build } from "esbuild";

const extensionRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const repositoryRoot = resolve(extensionRoot, "..", "..");
const artifactRoot = resolve(repositoryRoot, "artifacts", "renderer-extension");
const outputRoot = resolve(artifactRoot, "unpacked");
const relativeOutput = relative(artifactRoot, outputRoot);

if (relativeOutput.startsWith("..") || relativeOutput === "") {
  throw new Error(`Refusing to clean unexpected output path: ${outputRoot}`);
}

await rm(outputRoot, { recursive: true, force: true });
await mkdir(outputRoot, { recursive: true });

const entryPoints = {
  "background/service-worker": join(extensionRoot, "src", "background", "service-worker.ts"),
  "content/index": join(extensionRoot, "src", "content", "index.ts"),
};

await build({
  entryPoints,
  outdir: outputRoot,
  bundle: true,
  format: "esm",
  platform: "browser",
  target: "chrome120",
  sourcemap: false,
  minify: false,
  legalComments: "none",
  logLevel: "info",
});

const manifestSource = join(extensionRoot, "manifest.json");
const manifest = JSON.parse(await readFile(manifestSource, "utf8"));
if (manifest.manifest_version !== 3) {
  throw new Error("Renderer Extension must use Manifest V3.");
}

await cp(manifestSource, join(outputRoot, "manifest.json"));
console.log(`Renderer Extension unpacked build: ${outputRoot}`);
