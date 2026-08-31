// @vitest-environment happy-dom

import { beforeEach, describe, expect, it } from "vitest";
import {
  RENDERER_ATTRIBUTE,
  RENDERER_CLASS,
  renderSubtree,
} from "../src/core/dom-renderer";
import {
  ensureRendererStyles,
  RENDERER_ACTIVE_ATTRIBUTE,
} from "../src/core/renderer-styles";

describe("static display renderer", () => {
  beforeEach(() => { document.body.replaceChildren(); document.head.replaceChildren(); });

  it("wraps complete graphemes while preserving DOM text, selection and extraction", () => {
    const original = "Hello 🫩 ไทย 👩🏽‍💻 family 👨‍👩‍👧‍👦 1️⃣ 🇹🇭 🏴󠁧󠁢󠁥󠁮󠁧󠁿 end";
    const container = document.createElement("div");
    container.textContent = original;
    document.body.append(container);

    const result = renderSubtree(container);
    const wrappers = [...container.querySelectorAll(`[${RENDERER_ATTRIBUTE}="emoji"]`)];
    const range = document.createRange();
    range.selectNodeContents(container);
    const selection = window.getSelection();
    selection?.removeAllRanges();
    selection?.addRange(range);

    expect(result.wrappersCreated).toBe(6);
    expect(wrappers.map(wrapper => wrapper.textContent)).toEqual([
      "🫩", "👩🏽‍💻", "👨‍👩‍👧‍👦", "1️⃣", "🇹🇭", "🏴󠁧󠁢󠁥󠁮󠁧󠁿",
    ]);
    expect(container.textContent).toBe(original);
    expect(selection?.toString()).toBe(original);
  });

  it("does not wrap plain text or renderer-owned nodes twice", () => {
    document.body.innerHTML = "<p>ภาษาไทย English 123 ©</p><p>ใหม่ 🫯</p>";
    expect(renderSubtree(document.body).wrappersCreated).toBe(1);
    expect(renderSubtree(document.body).wrappersCreated).toBe(0);
    expect(document.querySelectorAll(`.${RENDERER_CLASS}`)).toHaveLength(1);
  });

  it.each(["script", "style", "noscript", "code", "pre", "textarea", "select"])(
    "skips the %s subtree",
    tag => {
      const element = document.createElement(tag);
      element.textContent = "ห้ามแตะ 🫯";
      document.body.append(element);
      expect(renderSubtree(element).wrappersCreated).toBe(0);
      expect(element.textContent).toBe("ห้ามแตะ 🫯");
    },
  );

  it("skips editable content and nested descendants", () => {
    document.body.innerHTML = '<div contenteditable="true"><span>กำลังพิมพ์ 🫯</span></div><div>ข้อความ 🫯</div>';
    const result = renderSubtree(document.body);
    expect(result.wrappersCreated).toBe(1);
    expect(result.skippedEditableNodes).toBe(1);
    expect(document.querySelector('[contenteditable="true"] span')?.childNodes[0]?.nodeType).toBe(Node.TEXT_NODE);
  });

  it("injects one scoped font style without changing surrounding typography", () => {
    const hostStyle = document.createElement("style");
    hostStyle.textContent = "span { display: block; line-height: 0; }";
    document.head.append(hostStyle);
    const first = ensureRendererStyles(document, "chrome-extension://example/assets/fonts/Noto-COLRv1.ttf");
    const second = ensureRendererStyles(document, "ignored.ttf");
    expect(first).toBe(second);
    expect(first.textContent).toContain(`.${RENDERER_CLASS}`);
    expect(first.textContent).toContain(`:root[${RENDERER_ACTIVE_ATTRIBUTE}] [data-e2e="dm-new-conversation-item"]`);
    expect(first.textContent).toContain('font-family: "ModernEmojiNotoDisplaySafe";');
    expect(first.textContent).toContain("unicode-range: U+0080-10FFFF;");
    expect(first.textContent).toContain('"ModernEmojiNotoDisplaySafe", "TikTokFont"');
    expect(first.textContent).not.toMatch(/(^|\n)\s*\*\s*\{/u);
    document.body.innerHTML = `
      <span><img height="16" width="16" alt="😆"
        src="https://static.xx.fbcdn.net/images/emoji.php/v9/t4/1/16/1f606.png"></span>`;
    renderSubtree(document.body);
    const imageWrapper = document.querySelector<HTMLElement>(`[${RENDERER_ATTRIBUTE}="emoji-image"]`)!;
    const computed = getComputedStyle(imageWrapper);
    expect(computed.display).toBe("inline-flex");
    expect(computed.lineHeight).toBe("1");
    expect(document.querySelectorAll("style")).toHaveLength(2);
  });
});
