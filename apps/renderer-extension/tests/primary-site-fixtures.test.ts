// @vitest-environment happy-dom

import { readFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { renderSubtree, RENDERER_ATTRIBUTE } from "../src/core/dom-renderer";
import { IncrementalRenderer } from "../src/core/incremental-renderer";

const fixtures = dirname(fileURLToPath(import.meta.url));

async function loadFixture(name: string): Promise<void> {
  const html = await readFile(join(fixtures, "fixtures", name), "utf8");
  document.open();
  document.write(html);
  document.close();
}

describe.each([
  ["Instagram DM", "instagram-dm.html", "[data-preview]", "[data-history]"],
  ["TikTok Chat", "tiktok-chat.html", "[data-e2e=chat-preview]", "[data-e2e=chat-history]"],
])("%s regression fixture", (_site, file, previewSelector, historySelector) => {
  it("renders sent, received and preview display content but not the composer", async () => {
    await loadFixture(file);
    const original = document.body.textContent;
    const result = renderSubtree(document.body);

    expect(result.wrappersCreated).toBe(9);
    expect(document.body.textContent).toBe(original);
    expect(document.querySelector(previewSelector)?.querySelector(`[${RENDERER_ATTRIBUTE}]`)).not.toBeNull();
    expect(document.querySelector('[contenteditable="true"]')?.querySelector(`[${RENDERER_ATTRIBUTE}]`)).toBeNull();
    expect(document.querySelector('[contenteditable="true"]')?.textContent).toBe("กำลังพิมพ์ภาษาไทย 🫯");
  });

  it("renders live messages and older history incrementally after initial scan", async () => {
    await loadFixture(file);
    const renderer = new IncrementalRenderer(document, { maxNodesPerBatch: 2 });
    renderer.start(document.body);
    renderer.flushSynchronously();
    const live = document.createElement("p");
    live.textContent = "ข้อความสด 🫯";
    const old = document.createElement("p");
    old.textContent = "ประวัติเก่า 👨‍👩‍👧‍👦";
    document.querySelector("main")?.append(live);
    document.querySelector(historySelector)?.prepend(old);
    await new Promise<void>(resolve => queueMicrotask(resolve));
    renderer.flushSynchronously();

    expect(live.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`)).toHaveLength(1);
    expect(old.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`)).toHaveLength(1);
    expect(document.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`)).toHaveLength(11);
    renderer.stop();
  });
});
