import {
  DEFAULT_SETTINGS,
  SETTINGS_STORAGE_KEY,
  migrateSettings,
  settingsEqual,
  type RendererSettings,
} from "./settings";

export async function loadSettings(): Promise<RendererSettings> {
  const stored = await chrome.storage.local.get(SETTINGS_STORAGE_KEY);
  const settings = migrateSettings(stored[SETTINGS_STORAGE_KEY]);
  if (!stored[SETTINGS_STORAGE_KEY] || !settingsEqual(settings, stored[SETTINGS_STORAGE_KEY] as RendererSettings)) {
    await chrome.storage.local.set({ [SETTINGS_STORAGE_KEY]: settings });
  }
  return settings;
}

export async function saveSettings(settings: RendererSettings): Promise<void> {
  await chrome.storage.local.set({ [SETTINGS_STORAGE_KEY]: migrateSettings(settings) });
}

export async function resetSettings(): Promise<RendererSettings> {
  const settings = { ...DEFAULT_SETTINGS, sites: [...DEFAULT_SETTINGS.sites] };
  await saveSettings(settings);
  return settings;
}
