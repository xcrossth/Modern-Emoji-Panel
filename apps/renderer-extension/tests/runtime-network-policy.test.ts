import { readFile } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

const extensionRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const repositoryRoot = resolve(extensionRoot, "..", "..");
const output = join(repositoryRoot, "artifacts", "renderer-extension", "unpacked");

describe("runtime network and remote-code policy", () => {
  it("contains no outbound network API or remote executable-code path in production bundles", async () => {
    for (const path of [
      "background/service-worker.js",
      "content/index.js",
      "popup/popup.js",
      "options/options.js",
    ]) {
      const script = await readFile(join(output, path), "utf8");
      expect(script).not.toMatch(/\bfetch\s*\(/u);
      expect(script).not.toMatch(/\bXMLHttpRequest\b|\bWebSocket\b|\bEventSource\b|\bimportScripts\b/u);
      expect(script).not.toMatch(/import\s*\(\s*["']https?:/u);
      expect(script).not.toContain("eval(");
    }
  });

  it("loads scripts, styles and fonts only from the packaged extension", async () => {
    const manifest = JSON.parse(await readFile(join(output, "manifest.json"), "utf8"));
    const css = await readFile(join(output, "assets", "styles", "renderer.css"), "utf8");
    const popup = await readFile(join(output, "popup", "popup.html"), "utf8");
    const options = await readFile(join(output, "options", "options.html"), "utf8");
    expect(manifest).not.toHaveProperty("update_url");
    expect(css).not.toMatch(/url\(\s*["']?https?:/u);
    expect(popup + options).not.toMatch(/<(?:script|link)[^>]+(?:src|href)=["']https?:/u);
  });
});
