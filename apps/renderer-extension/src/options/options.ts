import {
  DEFAULT_SETTINGS,
  SETTINGS_SCHEMA_VERSION,
  migrateSettings,
  normalizeSite,
  type RendererSettings,
  type SitePolicyMode,
} from "../settings/settings";
import { requiredOptionalOrigins } from "../settings/permissions";

interface SettingsResponse { ok: boolean; settings?: RendererSettings; error?: string }

const form = document.querySelector<HTMLFormElement>("#settings-form")!;
const enabled = document.querySelector<HTMLInputElement>("#enabled")!;
const mode = document.querySelector<HTMLSelectElement>("#mode")!;
const sites = document.querySelector<HTMLTextAreaElement>("#sites")!;
const rendererMode = document.querySelector<HTMLSelectElement>("#renderer-mode")!;
const dynamic = document.querySelector<HTMLInputElement>("#dynamic")!;
const debug = document.querySelector<HTMLInputElement>("#debug")!;
const reset = document.querySelector<HTMLButtonElement>("#reset")!;
const status = document.querySelector<HTMLElement>("#form-status")!;
document.querySelector<HTMLElement>("#extension-version")!.textContent = chrome.runtime.getManifest().version;
let current: RendererSettings = { ...DEFAULT_SETTINGS, sites: [...DEFAULT_SETTINGS.sites] };

function showStatus(text: string, error = false): void {
  status.textContent = text;
  status.classList.toggle("error", error);
}

function render(settings: RendererSettings): void {
  current = settings;
  enabled.checked = settings.enabled;
  mode.value = settings.mode;
  sites.value = settings.sites.join("\n");
  sites.disabled = settings.mode === "all";
  rendererMode.value = settings.rendererMode;
  dynamic.checked = settings.processDynamicContent;
  debug.checked = settings.debug;
}

function readSites(): string[] {
  const lines = sites.value.split(/\r?\n/u).map(line => line.trim()).filter(Boolean);
  const invalid = lines.filter(line => !normalizeSite(line));
  if (invalid.length) throw new Error(`Domain ไม่ถูกต้อง: ${invalid.join(", ")}`);
  return [...new Set(lines.map(line => normalizeSite(line)!))].sort();
}

function readForm(): RendererSettings {
  return {
    schemaVersion: SETTINGS_SCHEMA_VERSION,
    enabled: enabled.checked,
    mode: mode.value as SitePolicyMode,
    sites: mode.value === "all" ? [] : readSites(),
    rendererMode: "noto-colrv1",
    processDynamicContent: dynamic.checked,
    debug: debug.checked,
  };
}

async function reconcilePermissions(previous: RendererSettings, next: RendererSettings): Promise<void> {
  const before = requiredOptionalOrigins(previous);
  const after = requiredOptionalOrigins(next);
  const removed = before.filter(origin => !after.includes(origin));
  if (removed.length) await chrome.permissions.remove({ origins: removed });
  const missing: string[] = [];
  for (const origin of after) {
    if (!await chrome.permissions.contains({ origins: [origin] })) missing.push(origin);
  }
  if (missing.length && !await chrome.permissions.request({ origins: missing })) {
    throw new Error("ไม่ได้รับสิทธิ์เว็บไซต์ที่ต้องใช้ จึงยังไม่บันทึกการตั้งค่า");
  }
}

async function save(next: RendererSettings): Promise<RendererSettings> {
  await reconcilePermissions(current, next);
  const response = await chrome.runtime.sendMessage({ type: "settings:save", settings: next }) as SettingsResponse;
  if (!response.ok || !response.settings) throw new Error(response.error ?? "บันทึกไม่สำเร็จ");
  return migrateSettings(response.settings);
}

mode.addEventListener("change", () => { sites.disabled = mode.value === "all"; });
form.addEventListener("submit", event => {
  event.preventDefault();
  void (async () => {
    try {
      render(await save(readForm()));
      showStatus("บันทึกการตั้งค่าแล้ว");
    } catch (error) { showStatus(error instanceof Error ? error.message : String(error), true); }
  })();
});
reset.addEventListener("click", () => {
  void (async () => {
    try {
      await reconcilePermissions(current, DEFAULT_SETTINGS);
      const response = await chrome.runtime.sendMessage({ type: "settings:reset" }) as SettingsResponse;
      if (!response.ok || !response.settings) throw new Error(response.error ?? "คืนค่าไม่สำเร็จ");
      render(migrateSettings(response.settings));
      showStatus("คืนค่าเริ่มต้นแล้ว");
    } catch (error) { showStatus(error instanceof Error ? error.message : String(error), true); }
  })();
});

void (async () => {
  const response = await chrome.runtime.sendMessage({ type: "settings:get" }) as SettingsResponse;
  if (!response.ok || !response.settings) throw new Error(response.error ?? "โหลดการตั้งค่าไม่สำเร็จ");
  render(migrateSettings(response.settings));
})().catch(error => showStatus(error instanceof Error ? error.message : String(error), true));
