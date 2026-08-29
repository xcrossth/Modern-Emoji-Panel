import { readFile } from "node:fs/promises";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

const extensionRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const repositoryRoot = resolve(extensionRoot, "..", "..");
const output = join(repositoryRoot, "artifacts", "renderer-extension", "unpacked");

describe("popup and options package policy", () => {
  it("declares keyboard-accessible UI with broad host access remaining optional", async () => {
    const manifest = JSON.parse(await readFile(join(output, "manifest.json"), "utf8"));
    expect(manifest.permissions).toEqual(["storage", "activeTab", "scripting"]);
    expect(manifest.optional_host_permissions).toEqual(["<all_urls>"]);
    expect(manifest.action.default_popup).toBe("popup/popup.html");
    expect(manifest.options_ui).toEqual({ page: "options/options.html", open_in_tab: true });
    expect(manifest.host_permissions).not.toContain("<all_urls>");
  });

  it.each(["popup/popup.html", "options/options.html"])("builds %s without inline executable code", async path => {
    const html = await readFile(join(output, path), "utf8");
    expect(html).toMatch(/<html lang="th">/u);
    expect(html).toMatch(/<script type="module" src="\.\//u);
    expect(html).not.toMatch(/<script(?![^>]*\ssrc=)[^>]*>/u);
    expect(html).toContain("aria-live=\"polite\"");
  });
});
