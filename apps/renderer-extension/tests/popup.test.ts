// @vitest-environment happy-dom

import { readFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { DEFAULT_SETTINGS, withSiteEnabled, type RendererSettings } from "../src/settings/settings";

const popupHtml = await readFile(join(dirname(fileURLToPath(import.meta.url)), "..", "ui", "popup", "popup.html"), "utf8");

function loadMarkup(): void {
  document.open();
  document.write(
    popupHtml
      .replace(/<script[\s\S]*?<\/script>/u, "")
      .replace(/<link[^>]+rel="stylesheet"[^>]*>/u, ""),
  );
  document.close();
}

describe("popup", () => {
  beforeEach(() => { vi.resetModules(); vi.useFakeTimers(); loadMarkup(); });
  afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals(); });

  it("reads tab status and persists a per-site toggle through runtime messaging", async () => {
    let settings: RendererSettings = { ...DEFAULT_SETTINGS, sites: [...DEFAULT_SETTINGS.sites] };
    const messages: unknown[] = [];
    vi.stubGlobal("chrome", {
      tabs: {
        query: async () => [{ id: 7, url: "https://www.instagram.com/direct/inbox/" }],
        sendMessage: async () => ({ available: true, enabled: true, hostname: "instagram.com", wrappers: 12, metrics: null }),
      },
      runtime: {
        sendMessage: async (message: { type: string; hostname?: string; enabled?: boolean }) => {
          messages.push(message);
          if (message.type === "settings:get") return { ok: true, settings };
          if (message.type === "settings:set-site") {
            settings = withSiteEnabled(settings, message.hostname!, message.enabled!);
            return { ok: true, settings };
          }
          return { ok: false };
        },
        openOptionsPage: async () => undefined,
      },
      permissions: { request: async () => true },
      scripting: { insertCSS: async () => undefined, executeScript: async () => undefined },
    });

    await import("../src/popup/popup");
    await vi.waitFor(() => expect(document.querySelector<HTMLInputElement>("#site-enabled")?.checked).toBe(true));
    expect(document.querySelector("#fixed-count")?.textContent).toBe("12");
    const toggle = document.querySelector<HTMLInputElement>("#site-enabled")!;
    toggle.checked = false;
    toggle.dispatchEvent(new Event("change"));
    await vi.waitFor(() => expect(messages).toContainEqual({
      type: "settings:set-site", hostname: "instagram.com", enabled: false,
    }));
    expect(settings.sites).not.toContain("instagram.com");
  });

  it("disables controls on restricted Chrome pages", async () => {
    vi.stubGlobal("chrome", {
      tabs: { query: async () => [{ id: 8, url: "chrome://extensions/" }] },
      runtime: { sendMessage: async () => ({ ok: true, settings: DEFAULT_SETTINGS }), openOptionsPage: async () => undefined },
      permissions: { request: async () => false },
      scripting: {},
    });
    await import("../src/popup/popup");
    await vi.waitFor(() => expect(document.querySelector<HTMLInputElement>("#site-enabled")?.disabled).toBe(true));
    expect(document.querySelector("#site")?.textContent).toBe("หน้านี้ไม่รองรับ");
  });
});
