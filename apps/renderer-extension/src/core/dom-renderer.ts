import { segmentText } from "./emoji-segmenter";

export const RENDERER_ATTRIBUTE = "data-modern-emoji-renderer";
export const RENDERER_CLASS = "modern-emoji-renderer__emoji";
export const SOURCE_IMAGE_ATTRIBUTE = "data-modern-emoji-renderer-source-image";

const ORIGINAL_ARIA_HIDDEN_ATTRIBUTE = "data-modern-emoji-renderer-original-aria-hidden";
const MISSING_ATTRIBUTE_VALUE = "__missing__";

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

type NodeClassification = "render" | "skip" | "skip-editable";

function classifyAncestors(start: Node | null): NodeClassification {
  let current = start;
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

export function classifyTextNode(node: Text): NodeClassification {
  if (!node.data || !node.parentNode) return "skip";
  return classifyAncestors(node.parentNode);
}

function classifyElement(element: Element): NodeClassification {
  return classifyAncestors(element);
}

function hasSingleSupportedEmoji(value: string): boolean {
  const segments = segmentText(value);
  return segments.length === 1 && segments[0]?.isEmoji === true && segments[0].text === value;
}

export function isInstagramEmojiImage(element: Element): element is HTMLImageElement {
  if (element.tagName !== "IMG" || element.hasAttribute(SOURCE_IMAGE_ATTRIBUTE)) return false;
  const image = element as HTMLImageElement;
  const source = image.getAttribute("src");
  const alt = image.getAttribute("alt");
  if (!source || !alt || image.hidden || !hasSingleSupportedEmoji(alt)) return false;
  try {
    const url = new URL(source, image.ownerDocument.baseURI);
    const isInstagramCdn = url.hostname === "cdninstagram.com" || url.hostname.endsWith(".cdninstagram.com");
    return isInstagramCdn && /^\/images\/emoji\.php(?:\/|$)/u.test(url.pathname);
  } catch {
    return false;
  }
}

export function renderImageElement(element: Element): RenderResult {
  if (!isInstagramEmojiImage(element)) return { wrappersCreated: 0, skippedEditableNodes: 0 };
  const classification = classifyElement(element);
  if (classification !== "render") {
    return { wrappersCreated: 0, skippedEditableNodes: classification === "skip-editable" ? 1 : 0 };
  }

  const image = element;
  const document = image.ownerDocument;
  const wrapper = document.createElement("span");
  wrapper.className = RENDERER_CLASS;
  wrapper.setAttribute(RENDERER_ATTRIBUTE, "emoji-image");
  const originalAriaHidden = image.getAttribute("aria-hidden");
  image.setAttribute(
    ORIGINAL_ARIA_HIDDEN_ATTRIBUTE,
    originalAriaHidden === null ? MISSING_ATTRIBUTE_VALUE : originalAriaHidden,
  );
  image.setAttribute(SOURCE_IMAGE_ATTRIBUTE, "instagram-emoji");
  image.setAttribute("aria-hidden", "true");
  image.hidden = true;
  image.replaceWith(wrapper);
  wrapper.append(image, document.createTextNode(image.alt));
  return { wrappersCreated: 1, skippedEditableNodes: 0 };
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
  const parent = root as ParentNode;
  const images: Element[] = [];
  if (root.nodeType === root.ELEMENT_NODE && (root as Element).tagName === "IMG") images.push(root as Element);
  if (typeof parent.querySelectorAll === "function") images.push(...parent.querySelectorAll("img"));
  for (const image of images) {
    const result = renderImageElement(image);
    wrappersCreated += result.wrappersCreated;
    skippedEditableNodes += result.skippedEditableNodes;
  }
  return { wrappersCreated, skippedEditableNodes };
}

export function unwrapRenderedEmoji(root: ParentNode): number {
  const wrappers = Array.from(root.querySelectorAll<HTMLElement>(
    `[${RENDERER_ATTRIBUTE}="emoji"], [${RENDERER_ATTRIBUTE}="emoji-image"]`,
  ));
  const parents = new Set<ParentNode>();
  for (const wrapper of wrappers) {
    if (wrapper.parentNode) parents.add(wrapper.parentNode);
    if (wrapper.getAttribute(RENDERER_ATTRIBUTE) === "emoji-image") {
      const sourceImage = wrapper.querySelector<HTMLImageElement>(`img[${SOURCE_IMAGE_ATTRIBUTE}]`);
      if (sourceImage) {
        const originalAriaHidden = sourceImage.getAttribute(ORIGINAL_ARIA_HIDDEN_ATTRIBUTE);
        sourceImage.hidden = false;
        sourceImage.removeAttribute(SOURCE_IMAGE_ATTRIBUTE);
        sourceImage.removeAttribute(ORIGINAL_ARIA_HIDDEN_ATTRIBUTE);
        if (originalAriaHidden === MISSING_ATTRIBUTE_VALUE) sourceImage.removeAttribute("aria-hidden");
        else if (originalAriaHidden !== null) sourceImage.setAttribute("aria-hidden", originalAriaHidden);
        wrapper.replaceWith(sourceImage);
        continue;
      }
    }
    wrapper.replaceWith(wrapper.ownerDocument.createTextNode(wrapper.textContent ?? ""));
  }
  for (const parent of parents) parent.normalize();
  return wrappers.length;
}
