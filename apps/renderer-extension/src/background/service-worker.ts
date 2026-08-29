import { buildRegistrationPolicy } from "../settings/permissions";
import { migrateSettings, withSiteEnabled, type RendererSettings } from "../settings/settings";
import { loadSettings, resetSettings, saveSettings } from "../settings/storage";

const DYNAMIC_SCRIPT_ID = "modern-emoji-renderer-dynamic-sites";

async function synchronizeContentScripts(settings?: RendererSettings): Promise<void> {
  settings ??= await loadSettings();
  await chrome.scripting.unregisterContentScripts({ ids: [DYNAMIC_SCRIPT_ID] }).catch(() => undefined);
  const policy = buildRegistrationPolicy(settings);
  if (!policy) return;
  const registration: chrome.scripting.RegisteredContentScript = {
    id: DYNAMIC_SCRIPT_ID,
    matches: [...policy.matches],
    js: ["content/index.js"],
    css: ["assets/styles/renderer.css"],
    runAt: "document_start",
    persistAcrossSessions: true,
    ...(policy.excludeMatches.length ? { excludeMatches: [...policy.excludeMatches] } : {}),
  };
  await chrome.scripting.registerContentScripts([registration]);
}

async function initialize(): Promise<void> {
  const settings = await loadSettings();
  await synchronizeContentScripts(settings);
}

chrome.runtime.onInstalled.addListener(() => { void initialize(); });
chrome.runtime.onStartup.addListener(() => { void initialize(); });
chrome.storage.onChanged.addListener((changes, areaName) => {
  if (areaName === "local" && changes.rendererSettings) {
    void synchronizeContentScripts(migrateSettings(changes.rendererSettings.newValue));
  }
});

chrome.runtime.onMessage.addListener((message: unknown, _sender, sendResponse) => {
  const request = message as {
    type?: string;
    settings?: RendererSettings;
    hostname?: string;
    enabled?: boolean;
  };
  const respond = async () => {
    switch (request.type) {
      case "settings:get":
        return { ok: true, settings: await loadSettings() };
      case "settings:save": {
        const settings = migrateSettings(request.settings);
        await saveSettings(settings);
        return { ok: true, settings };
      }
      case "settings:reset":
        return { ok: true, settings: await resetSettings() };
      case "settings:set-site": {
        if (typeof request.hostname !== "string" || typeof request.enabled !== "boolean") {
          throw new Error("Missing site toggle fields");
        }
        const settings = withSiteEnabled(await loadSettings(), request.hostname, request.enabled);
        await saveSettings(settings);
        return { ok: true, settings };
      }
      default:
        return { ok: false, error: "Unknown request" };
    }
  };
  void respond().then(sendResponse, error => sendResponse({
    ok: false,
    error: error instanceof Error ? error.message : String(error),
  }));
  return true;
});

void initialize();
