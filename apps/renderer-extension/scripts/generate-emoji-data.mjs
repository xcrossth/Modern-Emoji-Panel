import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const extensionRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const repositoryRoot = resolve(extensionRoot, "..", "..");
const sourcePath = resolve(repositoryRoot, "data", "emoji-baseline", "17.0", "emoji.json");
const outputPath = resolve(extensionRoot, "src", "generated", "emoji-sequences.json");
const source = JSON.parse(await readFile(sourcePath, "utf8"));

if (source.schemaVersion !== 1 || !String(source.baselineId).startsWith("emoji-17.0_")) {
  throw new Error("Renderer requires the pinned Emoji Baseline 17.0 schema 1 source");
}

const orderedEntries = [...source.entries].sort((left, right) => left.order - right.order);
const sequences = orderedEntries.map(entry => entry.text);
if (new Set(sequences).size !== sequences.length) throw new Error("Emoji Baseline contains duplicate text sequences");
if (orderedEntries.some(entry => entry.qualification !== "fully-qualified")) {
  throw new Error("Renderer data must contain only fully-qualified Emoji sequences");
}

const generated = `${JSON.stringify({
  schemaVersion: 1,
  baselineId: source.baselineId,
  sequenceCount: sequences.length,
  sequences,
}, null, 2)}\n`;

if (process.argv.includes("--check")) {
  const existing = await readFile(outputPath, "utf8").catch(() => "");
  if (existing !== generated) throw new Error("Generated Emoji renderer data is stale; run npm run generate:data");
  process.stdout.write(`Emoji renderer data is current: ${sequences.length} sequences\n`);
} else {
  await mkdir(dirname(outputPath), { recursive: true });
  await writeFile(outputPath, generated);
  process.stdout.write(`Generated ${sequences.length} Emoji sequences: ${outputPath}\n`);
}
