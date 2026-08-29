import { isSiteEnabled, migrateSettings, normalizeSite, type RendererSettings } from "../settings/settings";
import { requiredOptionalOrigins, sitePattern } from "../settings/permissions";
import { DEFAULT_PRIMARY_SITES } from "../sites/site-context";
import type { RendererPageStatus } from "../content/controller";

interface SettingsResponse { ok: boolean; settings?: RendererSettings; error?: string }

const siteLabel = document.querySelector<HTMLElement>("#site")!;
const toggle = document.querySelector<HTMLInputElement>("#site-enabled")!;
const statusLabel = document.querySelector<HTMLElement>("#site-status")!;
const fixedCount = document.querySelector<HTMLElement>("#fixed-count")!;
const optionsButton = document.querySelector<HTMLButtonElement>("#open-options")!;
let tab: chrome.tabs.Tab | null = null;
let hostname: string | null = null;
let settings: RendererSettings | null = null;
let pageAvailable = false;

function showStatus(text: string, error = false): void {
  statusLabel.textContent = text;
  statusLabel.classList.toggle("error", error);
}

async function getSettings(): Promise<RendererSettings> {
  const response = await chrome.runtime.sendMessage({ type: "settings:get" }) as SettingsResponse;
  if (!response.ok || !response.settings) throw new Error(response.error ?? "โหลด settings ไม่สำเร็จ");
  return migrateSettings(response.settings);
}

async function getPageStatus(): Promise<RendererPageStatus | null> {
  if (!tab?.id) return null;
  try {
    return await chrome.tabs.sendMessage(tab.id, { type: "renderer:get-status" }) as RendererPageStatus;
  } catch {
    return null;
  }
}

async function refresh(): Promise<void> {
  if (!hostname || !settings) return;
  const page = await getPageStatus();
  pageAvailable = page?.available === true;
  toggle.checked = isSiteEnabled(settings, hostname);
  fixedCount.textContent = page ? page.wrappers.toLocaleString("th-TH") : "—";
  showStatus(page
    ? (page.enabled ? "Renderer ทำงานบนหน้านี้" : "Renderer ปิดอยู่บนหน้านี้")
    : (toggle.checked ? "เปิดใช้งานแล้ว—รีเฟรชหน้าเพื่อเริ่ม Renderer" : "Renderer ปิดอยู่บนหน้านี้"));
}

async function ensureSitePermission(site: string): Promise<void> {
  if (DEFAULT_PRIMARY_SITES.includes(site as (typeof DEFAULT_PRIMARY_SITES)[number])) return;
  if (settings && requiredOptionalOrigins(settings).includes("<all_urls>")) return;
  const granted = await chrome.permissions.request({ origins: [sitePattern(site)] });
  if (!granted) throw new Error("ไม่ได้รับสิทธิ์สำหรับเว็บไซต์นี้");
}

async function injectCurrentTabIfNeeded(): Promise<void> {
  if (!tab?.id || pageAvailable) return;
  await chrome.scripting.insertCSS({ target: { tabId: tab.id }, files: ["assets/styles/renderer.css"] });
  await chrome.scripting.executeScript({ target: { tabId: tab.id }, files: ["content/index.js"] });
}

toggle.addEventListener("change", async () => {
  if (!hostname || !settings) return;
  toggle.disabled = true;
  try {
    if (toggle.checked) await ensureSitePermission(hostname);
    const response = await chrome.runtime.sendMessage({
      type: "settings:set-site", hostname, enabled: toggle.checked,
    }) as SettingsResponse;
    if (!response.ok || !response.settings) throw new Error(response.error ?? "บันทึกสถานะไม่สำเร็จ");
    settings = migrateSettings(response.settings);
    if (toggle.checked) await injectCurrentTabIfNeeded();
    await refresh();
  } catch (error) {
    toggle.checked = !toggle.checked;
    showStatus(error instanceof Error ? error.message : String(error), true);
  } finally {
    toggle.disabled = false;
  }
});

optionsButton.addEventListener("click", () => { void chrome.runtime.openOptionsPage(); });

async function initialize(): Promise<void> {
  const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
  tab = tabs[0] ?? null;
  try {
    const url = tab?.url ? new URL(tab.url) : null;
    hostname = url ? normalizeSite(url.hostname) : null;
  } catch { hostname = null; }
  if (!hostname || !tab?.id) {
    siteLabel.textContent = "หน้านี้ไม่รองรับ";
    toggle.disabled = true;
    showStatus("Chrome ไม่อนุญาตให้ Extension ทำงานบนหน้านี้");
    return;
  }
  siteLabel.textContent = hostname;
  settings = await getSettings();
  await refresh();
  window.setInterval(() => { void refresh(); }, 750);
}

void initialize().catch(error => {
  toggle.disabled = true;
  showStatus(error instanceof Error ? error.message : String(error), true);
});
