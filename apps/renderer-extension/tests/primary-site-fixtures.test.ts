// @vitest-environment happy-dom

import { readFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import {
  renderSubtree,
  RENDERER_ATTRIBUTE,
  SOURCE_IMAGE_ATTRIBUTE,
  unwrapRenderedEmoji,
} from "../src/core/dom-renderer";
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

describe("Instagram image Emoji regression", () => {
  it("renders CDN Emoji images inside a text bubble with Noto while preserving ordinary images", () => {
    document.body.innerHTML = `
      <main aria-label="ห้องสนทนา">
        <div role="row" data-message-id="story-reply">
          <img data-story-thumbnail alt="🥺" src="data:image/gif;base64,R0lGODlhAQABAAAAACw=" />
          <span>ตอบสตอรี่
            <img height="16" width="16" alt="🥺" src="https://static.cdninstagram.com/images/emoji.php/v9/t73/1/16/1f979.png" />
            <img height="16" width="16" alt="❤️" src="https://static.cdninstagram.com/images/emoji.php/v9/t73/1/16/2764_fe0f.png" />
          </span>
        </div>
      </main>
    `;

    const result = renderSubtree(document.body);
    const reply = document.querySelector('[data-message-id="story-reply"]');
    const renderedEmoji = [...reply!.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`)]
      .map(element => element.textContent);

    expect(result.wrappersCreated).toBe(2);
    expect(renderedEmoji).toEqual(["🥺", "❤️"]);
    const sourceEmojiImages = [...reply!.querySelectorAll<HTMLImageElement>('img[src*="/images/emoji.php/"]')];
    expect(sourceEmojiImages).toHaveLength(2);
    expect(sourceEmojiImages.every(image => image.hidden)).toBe(true);
    expect(reply?.querySelector<HTMLImageElement>("[data-story-thumbnail]")?.hidden).toBe(false);
    expect(reply?.textContent).toContain("ตอบสตอรี่");
    expect(reply?.textContent).toContain("🥺");
    expect(reply?.textContent).toContain("❤️");

    expect(unwrapRenderedEmoji(document.body)).toBe(2);
    expect(reply?.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`)).toHaveLength(0);
    expect(sourceEmojiImages.every(image => !image.hidden && !image.hasAttribute(SOURCE_IMAGE_ATTRIBUTE))).toBe(true);
  });

  it("renders an Instagram CDN Emoji image added after the initial scan", async () => {
    document.body.innerHTML = '<main aria-label="ห้องสนทนา"></main>';
    const renderer = new IncrementalRenderer(document, { maxNodesPerBatch: 2 });
    renderer.start(document.body);
    renderer.flushSynchronously();

    const reply = document.createElement("div");
    reply.dataset.messageId = "new-reply";
    reply.innerHTML = `ข้อความใหม่
      <img height="16" width="16" alt="🥺" src="https://static.cdninstagram.com/images/emoji.php/v9/t73/1/16/1f979.png" />`;
    document.querySelector("main")?.append(reply);
    await new Promise<void>(resolve => queueMicrotask(resolve));
    renderer.flushSynchronously();

    expect(reply.querySelector(`[${RENDERER_ATTRIBUTE}]`)?.textContent).toBe("🥺");
    expect(reply.querySelector<HTMLImageElement>('img[src*="/images/emoji.php/"]')?.hidden).toBe(true);
    renderer.stop();
  });

  it("does not modify an Instagram Emoji image inside Editable Content", () => {
    document.body.innerHTML = `<div contenteditable="true">กำลังพิมพ์
      <img id="composer-emoji" height="16" width="16" alt="🥺" src="https://static.cdninstagram.com/images/emoji.php/v9/t73/1/16/1f979.png" />
    </div>`;

    const result = renderSubtree(document.body);
    const image = document.querySelector<HTMLImageElement>("#composer-emoji");

    expect(result.wrappersCreated).toBe(0);
    expect(result.skippedEditableNodes).toBeGreaterThanOrEqual(1);
    expect(image?.hidden).toBe(false);
    expect(document.querySelector(`[${RENDERER_ATTRIBUTE}]`)).toBeNull();
  });
});

describe("Facebook and Messenger image Emoji regression", () => {
  it("renders Meta CDN Emoji images in bubble text and reactions without touching photos or the composer", async () => {
    await loadFixture("facebook-messenger.html");
    const emojiOnlySource = document.querySelector<HTMLImageElement>('[data-message-id="emoji-only"] img');
    Object.defineProperty(emojiOnlySource, "offsetWidth", { value: 32 });
    Object.defineProperty(emojiOnlySource, "offsetHeight", { value: 32 });
    Object.defineProperty(emojiOnlySource, "getBoundingClientRect", {
      value: () => ({
        x: 0, y: 0, top: 0, right: 31.9965, bottom: 31.9965,
        left: 0, width: 31.9965, height: 31.9965, toJSON: () => ({}),
      }),
    });

    const result = renderSubtree(document.body);
    const bubble = document.querySelector('[data-message-id="bubble"]');
    const reaction = document.querySelector('[data-message-id="reaction"]');
    const sourceEmojiImages = [...document.querySelectorAll<HTMLImageElement>('img[src*="/images/emoji.php/"]')];

    expect(result.wrappersCreated).toBe(3);
    expect(bubble?.querySelector(`[${RENDERER_ATTRIBUTE}]`)?.textContent).toBe("😆");
    expect(reaction?.querySelector(`[${RENDERER_ATTRIBUTE}]`)?.textContent).toBe("🥰");
    expect(document.querySelector('[data-message-id="emoji-only"]')
      ?.querySelector(`[${RENDERER_ATTRIBUTE}]`)?.textContent).toBe("🥰");
    const emojiOnlyWrapper = document.querySelector<HTMLElement>(
      '[data-message-id="emoji-only"] [data-modern-emoji-renderer]',
    );
    expect(emojiOnlyWrapper?.style.fontSize).toBe("32px");
    expect(emojiOnlyWrapper?.style.width).toBe("32px");
    expect(emojiOnlyWrapper?.style.height).toBe("32px");
    expect(sourceEmojiImages.filter(image => image.hidden)).toHaveLength(3);
    expect(document.querySelector<HTMLImageElement>("[data-profile-photo]")?.hidden).toBe(false);
    expect(document.querySelector<HTMLImageElement>("#composer-emoji")?.hidden).toBe(false);
  });

  it("preserves the pre-transform layout box instead of applying an ancestor scale twice", () => {
    document.body.innerHTML = `<div style="transform: scale(0.357)">
      <img id="scaled-emoji" height="56" width="56" style="width: 20px; height: 20px" alt="🦛"
        src="https://static.xx.fbcdn.net/images/emoji.php/v9/t1/1/56/1f99b.png">
    </div>`;
    const source = document.querySelector<HTMLImageElement>("#scaled-emoji")!;
    Object.defineProperty(source, "offsetWidth", { value: 20 });
    Object.defineProperty(source, "offsetHeight", { value: 20 });
    Object.defineProperty(source, "getBoundingClientRect", {
      value: () => ({
        x: 0, y: 0, top: 0, right: 20, bottom: 20,
        left: 0, width: 20, height: 20, toJSON: () => ({}),
      }),
    });

    renderSubtree(document.body);
    const wrapper = document.querySelector<HTMLElement>(`[${RENDERER_ATTRIBUTE}="emoji-image"]`)!;

    expect(wrapper.style.width).toBe("56px");
    expect(wrapper.style.height).toBe("56px");
    expect(wrapper.style.fontSize).toBe("56px");
  });

  it("keeps the wrapper in sync when Meta updates the image size after the initial render", async () => {
    document.body.innerHTML = `<div style="transform: scale(0.357)">
      <img id="growing-emoji" height="20" width="20" style="width: 20px; height: 20px" alt="🦛"
        src="https://static.xx.fbcdn.net/images/emoji.php/v9/t1/1/56/1f99b.png">
    </div>`;
    const renderer = new IncrementalRenderer(document);
    renderer.start(document.body);
    renderer.flushSynchronously();
    const source = document.querySelector<HTMLImageElement>(`#growing-emoji[${SOURCE_IMAGE_ATTRIBUTE}]`)!;
    const wrapper = source.parentElement!;

    expect(wrapper.style.width).toBe("20px");
    source.setAttribute("width", "56");
    source.setAttribute("height", "56");
    await new Promise<void>(resolve => queueMicrotask(resolve));

    expect(wrapper.style.width).toBe("56px");
    expect(wrapper.style.height).toBe("56px");
    expect(wrapper.style.fontSize).toBe("56px");
    renderer.stop();
  });

  it("renders a Facebook reaction added after the initial scan", async () => {
    document.body.innerHTML = '<main aria-label="ห้องสนทนา"></main>';
    const renderer = new IncrementalRenderer(document, { maxNodesPerBatch: 2 });
    renderer.start(document.body);
    renderer.flushSynchronously();

    const reaction = document.createElement("div");
    reaction.setAttribute("aria-label", "1 reaction with 🥰");
    reaction.innerHTML = '<img height="16" width="16" alt="🥰" src="https://static.xx.fbcdn.net/images/emoji.php/v9/t91/1/16/1f970.png">';
    document.querySelector("main")?.append(reaction);
    await new Promise<void>(resolve => queueMicrotask(resolve));
    renderer.flushSynchronously();

    expect(reaction.querySelector(`[${RENDERER_ATTRIBUTE}]`)?.textContent).toBe("🥰");
    expect(reaction.querySelector<HTMLImageElement>('img[src*="/images/emoji.php/"]')?.hidden).toBe(true);
    renderer.stop();
  });
});
