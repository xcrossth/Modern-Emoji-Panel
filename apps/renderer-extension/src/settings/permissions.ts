import { DEFAULT_PRIMARY_SITES } from "../sites/site-context";
import type { RendererSettings } from "./settings";

const primaryPatterns = DEFAULT_PRIMARY_SITES.flatMap(site => [`https://www.${site}/*`]);

export function sitePattern(site: string): string {
  return `*://*.${site}/*`;
}

export function requiredOptionalOrigins(settings: RendererSettings): readonly string[] {
  if (!settings.enabled) return [];
  if (settings.mode === "all" || settings.mode === "denylist") return ["<all_urls>"];
  return settings.sites
    .filter(site => !DEFAULT_PRIMARY_SITES.includes(site as (typeof DEFAULT_PRIMARY_SITES)[number]))
    .map(sitePattern);
}

export interface RegistrationPolicy {
  readonly matches: readonly string[];
  readonly excludeMatches: readonly string[];
}

export function buildRegistrationPolicy(settings: RendererSettings): RegistrationPolicy | null {
  if (!settings.enabled) return null;
  if (settings.mode === "allowlist") {
    const matches = requiredOptionalOrigins(settings);
    return matches.length ? { matches, excludeMatches: [] } : null;
  }
  const denied = settings.mode === "denylist" ? settings.sites.map(sitePattern) : [];
  return { matches: ["<all_urls>"], excludeMatches: [...primaryPatterns, ...denied] };
}
