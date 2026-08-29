// @vitest-environment happy-dom

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ContentRendererController } from "../src/content/controller";
import { DEFAULT_SETTINGS, SETTINGS_STORAGE_KEY } from "../src/settings/settings";
import { RENDERER_ATTRIBUTE } from "../src/core/dom-renderer";

describe("content renderer settings synchronization", () => {
  type StorageListener = (changes: Record<string, chrome.storage.StorageChange>, area: string) => void;
  const listeners = new Set<StorageListener>();
  let stored: unknown;

  beforeEach(() => {
    vi.useFakeTimers();
    document.body.innerHTML = "<article>ข้อความ 🫯</article><div contenteditable=true>composer 🫯</div>";
    stored = { ...DEFAULT_SETTINGS, sites: [...DEFAULT_SETTINGS.sites] };
    vi.stubGlobal("chrome", {
      storage: {
        local: {
          get: async () => ({ [SETTINGS_STORAGE_KEY]: stored }),
          set: async (value: Record<string, unknown>) => { stored = value[SETTINGS_STORAGE_KEY]; },
        },
        onChanged: {
          addListener: (listener: StorageListener) => listeners.add(listener),
          removeListener: (listener: StorageListener) => listeners.delete(listener),
        },
      },
    });
  });

  afterEach(() => { listeners.clear(); vi.useRealTimers(); vi.unstubAllGlobals(); });

  it("persists one controller, reacts to storage changes and restores exact Unicode when disabled", async () => {
    const controller = new ContentRendererController(document, "instagram.com");
    await controller.start();
    await vi.runAllTimersAsync();
    expect(document.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`)).toHaveLength(1);
    expect(document.body.textContent).toBe("ข้อความ 🫯composer 🫯");

    const disabled = { ...DEFAULT_SETTINGS, enabled: false, sites: [...DEFAULT_SETTINGS.sites] };
    for (const listener of listeners) {
      listener({ [SETTINGS_STORAGE_KEY]: { oldValue: stored, newValue: disabled } }, "local");
    }
    await Promise.resolve();
    expect(document.querySelectorAll(`[${RENDERER_ATTRIBUTE}]`)).toHaveLength(0);
    expect(document.body.textContent).toBe("ข้อความ 🫯composer 🫯");

    const disabledStatus = controller.status();
    expect(disabledStatus.enabled).toBe(false);
    expect(disabledStatus.wrappers).toBe(0);
    expect(JSON.stringify(disabledStatus)).not.toContain("🫯");

    for (const listener of listeners) {
      listener({ [SETTINGS_STORAGE_KEY]: { oldValue: disabled, newValue: DEFAULT_SETTINGS } }, "local");
    }
    await Promise.resolve();
    await vi.runAllTimersAsync();
    const live = document.createElement("p");
    live.textContent = "ข้อความใหม่ 🫯";
    document.body.append(live);
    await Promise.resolve();
    await vi.runAllTimersAsync();
    expect(controller.status().wrappers).toBe(2);
    expect(JSON.stringify(controller.status())).not.toContain("ข้อความใหม่");
    controller.stop();
    expect(listeners.size).toBe(0);
  });
});
