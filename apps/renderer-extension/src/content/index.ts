import { ContentRendererController } from "./controller";
import { identifyPrimarySite } from "../sites/site-context";

function startRenderer(): void {
  const runtimeWindow = window as typeof window & {
    __modernEmojiRendererController?: ContentRendererController;
  };
  if (runtimeWindow.__modernEmojiRendererController) return;
  const site = identifyPrimarySite(new URL(location.href));
  const hostname = location.hostname.replace(/^www\./u, "");
  if (!site && !hostname) return;
  const controller = new ContentRendererController(document, hostname);
  runtimeWindow.__modernEmojiRendererController = controller;
  chrome.runtime.onMessage.addListener((message: unknown, _sender, sendResponse) => {
    if ((message as { type?: string })?.type !== "renderer:get-status") return false;
    sendResponse(controller.status());
    return false;
  });
  void controller.start();
}

if (document.documentElement) startRenderer();
else document.addEventListener("DOMContentLoaded", startRenderer, { once: true });
