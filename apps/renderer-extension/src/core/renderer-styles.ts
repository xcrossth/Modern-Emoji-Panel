import { RENDERER_ATTRIBUTE, RENDERER_CLASS } from "./dom-renderer";

export const STYLE_ELEMENT_ID = "modern-emoji-renderer-styles";
export const FONT_FAMILY = "ModernEmojiNoto";

export function rendererStyleText(fontUrl: string): string {
  return `
@font-face {
  font-family: "${FONT_FAMILY}";
  src: url("${fontUrl}") format("truetype");
  font-display: block;
}
.${RENDERER_CLASS}[${RENDERER_ATTRIBUTE}="emoji"] {
  display: inline;
  font-family: "${FONT_FAMILY}", "Segoe UI Emoji", sans-serif !important;
  font-size: 1em;
  line-height: inherit;
  font-weight: normal !important;
  font-style: normal !important;
  font-variant: normal !important;
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
