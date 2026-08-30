import {
  RENDERER_ATTRIBUTE,
  RENDERER_CLASS,
  TIKTOK_VIRTUALIZED_CONVERSATION_ITEM_SELECTORS,
} from "./dom-renderer";

export const STYLE_ELEMENT_ID = "modern-emoji-renderer-styles";
export const FONT_FAMILY = "ModernEmojiNoto";
export const RENDERER_ACTIVE_ATTRIBUTE = "data-modern-emoji-renderer-active";

export function rendererStyleText(fontUrl: string): string {
  const tiktokConversationSelectors = TIKTOK_VIRTUALIZED_CONVERSATION_ITEM_SELECTORS
    .flatMap(selector => [
      `:root[${RENDERER_ACTIVE_ATTRIBUTE}] ${selector}`,
      `:root[${RENDERER_ACTIVE_ATTRIBUTE}] ${selector} *`,
    ])
    .join(",\n");
  return `
@font-face {
  font-family: "${FONT_FAMILY}";
  src: url("${fontUrl}") format("truetype");
  font-display: block;
}
.${RENDERER_CLASS}[${RENDERER_ATTRIBUTE}] {
  display: inline;
  font-family: "${FONT_FAMILY}", "Segoe UI Emoji", sans-serif !important;
  font-size: 1em;
  line-height: inherit;
  font-weight: normal !important;
  font-style: normal !important;
  font-variant: normal !important;
}
.${RENDERER_CLASS}[${RENDERER_ATTRIBUTE}="emoji-image"] {
  display: inline-flex !important;
  align-items: center !important;
  justify-content: center !important;
  flex: 0 0 auto !important;
  line-height: 1 !important;
  overflow: visible !important;
}
${tiktokConversationSelectors} {
  font-family: "${FONT_FAMILY}", "TikTokFont", Arial, Tahoma, sans-serif !important;
}
`.trim();
}

export function ensureRendererStyles(document: Document, fontUrl: string): HTMLStyleElement {
  const existing = document.getElementById(STYLE_ELEMENT_ID);
  if (existing?.tagName === "STYLE") return existing as HTMLStyleElement;
  const style = document.createElement("style");
  style.id = STYLE_ELEMENT_ID;
  style.textContent = rendererStyleText(fontUrl);
  (document.head ?? document.documentElement).append(style);
  return style;
}
