import { DEFAULT_PRIMARY_SITES } from "../sites/site-context";

export const SETTINGS_SCHEMA_VERSION = 2;
export const SETTINGS_STORAGE_KEY = "rendererSettings";

export type SitePolicyMode = "allowlist" | "denylist" | "all";
export type RendererMode = "noto-colrv1";

export interface RendererSettings {
  readonly schemaVersion: 2;
  readonly enabled: boolean;
  readonly mode: SitePolicyMode;
  readonly sites: readonly string[];
  readonly rendererMode: RendererMode;
  readonly processDynamicContent: boolean;
  readonly debug: boolean;
}

export const DEFAULT_SETTINGS: RendererSettings = Object.freeze({
  schemaVersion: SETTINGS_SCHEMA_VERSION,
  enabled: true,
  mode: "allowlist",
  sites: [...DEFAULT_PRIMARY_SITES],
  rendererMode: "noto-colrv1",
  processDynamicContent: true,
  debug: false,
});

export function normalizeSite(input: string): string | null {
  const trimmed = input.trim().toLowerCase().replace(/^\*\./u, "");
  if (!trimmed || /\s/u.test(trimmed)) return null;
  try {
    const url = new URL(trimmed.includes("://") ? trimmed : `https://${trimmed}`);
    if (!/^https?:$/u.test(url.protocol) || url.username || url.password) return null;
    const hostname = url.hostname.replace(/^www\./u, "").replace(/\.$/u, "");
    if (!hostname.includes(".") || hostname.startsWith(".") || hostname.endsWith(".")) return null;
    return hostname;
  } catch {
    return null;
  }
}

function normalizeSites(value: unknown): string[] {
  if (!Array.isArray(value)) return [...DEFAULT_SETTINGS.sites];
  const sites = new Set(value.flatMap(item => {
    const normalized = typeof item === "string" ? normalizeSite(item) : null;
    return normalized ? [normalized] : [];
  }));
  return [
    ...DEFAULT_PRIMARY_SITES.filter(site => sites.delete(site)),
    ...[...sites].sort(),
  ];
}

const VERSION_1_DEFAULT_SITES = ["instagram.com", "tiktok.com"] as const;

function isVersion1DefaultAllowlist(legacy: Record<string, unknown>, sites: readonly string[]): boolean {
  if (legacy.schemaVersion === SETTINGS_SCHEMA_VERSION || legacy.mode === "denylist" || legacy.mode === "all") {
    return false;
  }
  return sites.length === VERSION_1_DEFAULT_SITES.length
    && VERSION_1_DEFAULT_SITES.every(site => sites.includes(site));
}

export function migrateSettings(value: unknown): RendererSettings {
  if (!value || typeof value !== "object") return { ...DEFAULT_SETTINGS, sites: [...DEFAULT_SETTINGS.sites] };
  const legacy = value as Record<string, unknown>;
  const mode: SitePolicyMode = legacy.mode === "denylist" || legacy.mode === "all" ? legacy.mode : "allowlist";
  const normalizedSites = normalizeSites(legacy.sites);
  return {
    schemaVersion: SETTINGS_SCHEMA_VERSION,
    enabled: typeof legacy.enabled === "boolean" ? legacy.enabled : DEFAULT_SETTINGS.enabled,
    mode,
    sites: isVersion1DefaultAllowlist(legacy, normalizedSites)
      ? [...DEFAULT_PRIMARY_SITES]
      : normalizedSites,
    rendererMode: "noto-colrv1",
    processDynamicContent: typeof legacy.processDynamicContent === "boolean"
      ? legacy.processDynamicContent : DEFAULT_SETTINGS.processDynamicContent,
    debug: typeof legacy.debug === "boolean" ? legacy.debug : DEFAULT_SETTINGS.debug,
  };
}

export function isSiteEnabled(settings: RendererSettings, hostname: string): boolean {
  if (!settings.enabled) return false;
  const site = normalizeSite(hostname);
  if (!site) return false;
  if (settings.mode === "all") return true;
  const listed = settings.sites.includes(site);
  return settings.mode === "allowlist" ? listed : !listed;
}

export function withSiteEnabled(
  settings: RendererSettings,
  hostname: string,
  enabled: boolean,
): RendererSettings {
  const site = normalizeSite(hostname);
  if (!site) throw new Error("Invalid site hostname");
  const sites = new Set(settings.sites);
  let mode = settings.mode;
  if (mode === "all" && !enabled) {
    mode = "denylist";
    sites.clear();
  }
  const listedWhenEnabled = mode === "allowlist";
  if (enabled === listedWhenEnabled) sites.add(site); else sites.delete(site);
  return { ...settings, mode, sites: [...sites].sort() };
}

export function settingsEqual(left: RendererSettings, right: RendererSettings): boolean {
  return JSON.stringify(left) === JSON.stringify(right);
}
