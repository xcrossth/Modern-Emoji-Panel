import { readFile, stat } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

const extensionRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const repositoryRoot = resolve(extensionRoot, "..", "..");
const outputRoot = join(repositoryRoot, "artifacts", "renderer-extension", "unpacked");

const readOutput = (path: string) => readFile(join(outputRoot, path), "utf8");

describe("Renderer Extension foundation", () => {
  it("builds a minimal Manifest V3 package for only the two primary chat sites", async () => {
    const manifest = JSON.parse(await readOutput("manifest.json"));

    expect(manifest.manifest_version).toBe(3);
    expect(manifest.permissions).toEqual(["storage"]);
    expect(manifest.host_permissions).toEqual([
      "https://www.instagram.com/*",
      "https://www.tiktok.com/*",
    ]);
    expect(manifest.host_permissions).not.toContain("<all_urls>");
    expect(manifest.content_scripts).toHaveLength(1);
    expect(manifest.content_scripts[0].matches).toEqual(manifest.host_permissions);
  });

  it("emits every script referenced by the manifest", async () => {
    const manifest = JSON.parse(await readOutput("manifest.json"));
    const referencedScripts = [
      manifest.background.service_worker,
      ...manifest.content_scripts.flatMap((entry: { js: string[] }) => entry.js),
    ];

    for (const path of referencedScripts) {
      await expect(stat(join(outputRoot, path))).resolves.toMatchObject({ size: expect.any(Number) });
    }
  });

  it("contains no remote module import, eval, or source map in production scripts", async () => {
    for (const path of ["background/service-worker.js", "content/index.js"]) {
      const script = await readOutput(path);
      expect(script).not.toMatch(/import\s+[^;]*["']https?:/u);
      expect(script).not.toContain("eval(");
      expect(script).not.toContain("sourceMappingURL");
    }
  });
});
