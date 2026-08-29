// @vitest-environment happy-dom

import { beforeEach, describe, expect, it } from "vitest";
import { renderSubtree, RENDERER_ATTRIBUTE } from "../src/core/dom-renderer";

const allSites = [
  "Instagram feed/comments",
  "Google",
  "GitHub",
  "Reddit",
  "Facebook",
  "Discord Web",
];

describe("all-sites regression fixtures", () => {
  beforeEach(() => document.body.replaceChildren());

  it.each(allSites)("preserves typography and editable behavior for %s", site => {
    document.body.innerHTML = `
      <article data-site="${site}" style="font-family: Arial; font-size: 17px">ไทย 🫯 English 👩🏽‍💻</article>
      <div role="textbox" contenteditable="true">composer ไทย 🫯</div>`;
    const article = document.querySelector<HTMLElement>("article")!;
    const editor = document.querySelector<HTMLElement>("[contenteditable]")!;
    const originalText = document.body.textContent;
    const originalStyle = article.getAttribute("style");

    renderSubtree(document.body);

    expect(document.body.textContent).toBe(originalText);
    expect(article.getAttribute("style")).toBe(originalStyle);
    expect(article.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`)).toHaveLength(2);
    expect(editor.querySelector(`[${RENDERER_ATTRIBUTE}]`)).toBeNull();
    for (const wrapper of article.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`)) {
      expect(wrapper.getAttribute("role")).toBeNull();
      expect(wrapper.getAttribute("aria-label")).toBeNull();
      expect(wrapper.getAttribute("aria-hidden")).toBeNull();
    }
  });
});

describe("Editable Content interaction boundary", () => {
  it("preserves caret, selection and composition DOM, then renders only submitted display text", () => {
    document.body.innerHTML = '<div id="editor" role="textbox" contenteditable="true">ภาษาไทย 🫯 ทดสอบ</div><section id="messages"></section>';
    const editor = document.querySelector<HTMLElement>("#editor")!;
    const textNode = editor.firstChild!;
    const range = document.createRange();
    range.setStart(textNode, 7);
    range.setEnd(textNode, 12);
    const selection = window.getSelection()!;
    selection.removeAllRanges();
    selection.addRange(range);
    const selectedText = selection.toString();
    const originalHtml = editor.innerHTML;
    for (const type of ["compositionstart", "compositionupdate", "compositionend", "beforeinput", "input"]) {
      editor.dispatchEvent(new Event(type, { bubbles: true, cancelable: true }));
      renderSubtree(document.body);
    }

    expect(editor.innerHTML).toBe(originalHtml);
    expect(selection.toString()).toBe(selectedText);
    expect(selection.anchorNode).toBe(textNode);
    const posted = document.createElement("p");
    posted.textContent = editor.textContent;
    document.querySelector("#messages")!.append(posted);
    renderSubtree(posted);
    expect(editor.innerHTML).toBe(originalHtml);
    expect(posted.textContent).toBe(editor.textContent);
    expect(posted.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`)).toHaveLength(1);
  });
});
