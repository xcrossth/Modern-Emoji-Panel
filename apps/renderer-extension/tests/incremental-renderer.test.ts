// @vitest-environment happy-dom

import { beforeEach, describe, expect, it } from "vitest";
import { IncrementalRenderer } from "../src/core/incremental-renderer";
import { RENDERER_ATTRIBUTE } from "../src/core/dom-renderer";

const settleMutations = () => new Promise<void>(resolve => queueMicrotask(resolve));

describe("incremental DOM renderer", () => {
  beforeEach(() => document.body.replaceChildren());

  it("processes initial, added and changed display content without duplicate wrappers", async () => {
    document.body.innerHTML = "<main><p id=message>เดิม 🫩</p></main>";
    const renderer = new IncrementalRenderer(document, { maxNodesPerBatch: 2 });
    renderer.start(document.body);
    renderer.flushSynchronously();

    const added = document.createElement("p");
    added.textContent = "รับใหม่ 👩🏽‍💻";
    document.querySelector("main")?.append(added);
    const changed = document.createElement("p");
    changed.textContent = "ยังไม่มี";
    document.querySelector("main")?.append(changed);
    await settleMutations();
    renderer.flushSynchronously();
    changed.firstChild!.textContent = "แก้แล้ว 🫯";
    await settleMutations();
    renderer.flushSynchronously();

    expect(document.body.textContent).toBe("เดิม 🫩รับใหม่ 👩🏽‍💻แก้แล้ว 🫯");
    expect(document.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`)).toHaveLength(3);
    expect(renderer.metrics.wrappersCreated).toBe(3);
    renderer.start(document.body);
    renderer.flushSynchronously();
    expect(document.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`)).toHaveLength(3);
    renderer.stop();
  });

  it("splits a long transcript and mutation burst into bounded batches", () => {
    const transcript = document.createDocumentFragment();
    for (let index = 0; index < 1_000; index += 1) {
      const message = document.createElement("p");
      message.textContent = `ข้อความ ${index} 🫩`;
      transcript.append(message);
    }
    document.body.append(transcript);
    const renderer = new IncrementalRenderer(document, { maxNodesPerBatch: 50 });
    renderer.start(document.body);
    renderer.flushSynchronously();

    expect(renderer.metrics.nodesVisited).toBe(1_000);
    expect(renderer.metrics.wrappersCreated).toBe(1_000);
    expect(renderer.metrics.batches).toBeGreaterThanOrEqual(20);
    expect(document.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`)).toHaveLength(1_000);
    renderer.stop();
  });

  it("records editable skips in debug metrics without modifying the editor", () => {
    document.body.innerHTML = '<div contenteditable="true">composer 🫯</div><article>posted 🫯</article>';
    const renderer = new IncrementalRenderer(document, { debug: false });
    renderer.start(document.body);
    renderer.flushSynchronously();

    expect(renderer.metrics.skippedEditableNodes).toBe(1);
    expect(document.querySelector("[contenteditable]")?.textContent).toBe("composer 🫯");
    expect(document.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`)).toHaveLength(1);
    renderer.stop();
  });
});
