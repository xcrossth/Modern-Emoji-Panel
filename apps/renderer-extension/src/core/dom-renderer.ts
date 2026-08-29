import { segmentText } from "./emoji-segmenter";

export const RENDERER_ATTRIBUTE = "data-modern-emoji-renderer";
export const RENDERER_CLASS = "modern-emoji-renderer__emoji";

const SKIPPED_TAGS = new Set([
  "SCRIPT", "STYLE", "NOSCRIPT", "INPUT", "TEXTAREA", "CODE", "PRE", "SELECT", "OPTION",
]);

export interface RenderResult {
  readonly wrappersCreated: number;
  readonly skippedEditableNodes: number;
}

function hasEditableState(element: Element): boolean {
  const value = element.getAttribute("contenteditable");
  return value !== null && value.toLowerCase() !== "false";
}

export function classifyTextNode(node: Text): "render" | "skip" | "skip-editable" {
  if (!node.data || !node.parentNode) return "skip";
  let current: Node | null = node.parentNode;
  while (current) {
    if (current.nodeType === current.ELEMENT_NODE) {
      const element = current as Element;
      if (element.hasAttribute(RENDERER_ATTRIBUTE)) return "skip";
      if (SKIPPED_TAGS.has(element.tagName)) return "skip";
      if (hasEditableState(element) || (element as HTMLElement).isContentEditable) return "skip-editable";
    }
    current = current.parentNode;
  }
  return "render";
}

export function renderTextNode(node: Text): RenderResult {
  const classification = classifyTextNode(node);
  if (classification !== "render") {
    return { wrappersCreated: 0, skippedEditableNodes: classification === "skip-editable" ? 1 : 0 };
  }

  const segments = segmentText(node.data);
  if (!segments.some(segment => segment.isEmoji)) {
    return { wrappersCreated: 0, skippedEditableNodes: 0 };
  }

  const document = node.ownerDocument;
  const fragment = document.createDocumentFragment();
  let wrappersCreated = 0;
  for (const segment of segments) {
    if (!segment.isEmoji) {
      fragment.append(document.createTextNode(segment.text));
      continue;
    }
    const wrapper = document.createElement("span");
    wrapper.className = RENDERER_CLASS;
    wrapper.setAttribute(RENDERER_ATTRIBUTE, "emoji");
    wrapper.textContent = segment.text;
    fragment.append(wrapper);
    wrappersCreated += 1;
  }
  node.replaceWith(fragment);
  return { wrappersCreated, skippedEditableNodes: 0 };
}

export function collectTextNodes(root: Node): Text[] {
  if (root.nodeType === root.TEXT_NODE) return [root as Text];
  const document = root.ownerDocument ?? (root as Document);
  const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
  const nodes: Text[] = [];
  let current = walker.nextNode();
  while (current) {
    nodes.push(current as Text);
    current = walker.nextNode();
  }
  return nodes;
}

export function renderSubtree(root: Node): RenderResult {
  let wrappersCreated = 0;
  let skippedEditableNodes = 0;
  for (const node of collectTextNodes(root)) {
    const result = renderTextNode(node);
    wrappersCreated += result.wrappersCreated;
    skippedEditableNodes += result.skippedEditableNodes;
  }
  return { wrappersCreated, skippedEditableNodes };
}
