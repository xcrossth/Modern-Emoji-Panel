// @vitest-environment happy-dom

import { beforeEach, describe, expect, it } from "vitest";
import {
  RENDERER_ATTRIBUTE,
  RENDERER_CLASS,
  renderSubtree,
} from "../src/core/dom-renderer";
import { ensureRendererStyles } from "../src/core/renderer-styles";

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
    const first = ensureRendererStyles(document, "chrome-extension://example/assets/fonts/Noto-COLRv1.ttf");
    const second = ensureRendererStyles(document, "ignored.ttf");
    expect(first).toBe(second);
    expect(first.textContent).toContain(`.${RENDERER_CLASS}`);
    expect(first.textContent).not.toMatch(/(^|\n)\s*\*\s*\{/u);
    expect(document.querySelectorAll("style")).toHaveLength(1);
  });
});
