export type PrimarySiteId = "instagram.com" | "tiktok.com" | "facebook.com" | "messenger.com";

export interface SiteContext {
  readonly id: PrimarySiteId;
  readonly isPrimaryChatRoute: boolean;
}

export const DEFAULT_PRIMARY_SITES: readonly PrimarySiteId[] = [
  "instagram.com",
  "tiktok.com",
  "facebook.com",
  "messenger.com",
];

function registrableHost(hostname: string): string {
  return hostname.toLowerCase().replace(/^www\./u, "");
}

export function identifyPrimarySite(url: URL): SiteContext | null {
  const host = registrableHost(url.hostname);
  if (host === "instagram.com") {
    return { id: host, isPrimaryChatRoute: /^\/direct(?:\/|$)/u.test(url.pathname) };
  }
  if (host === "tiktok.com") {
    return { id: host, isPrimaryChatRoute: /^\/(?:messages?|inbox)(?:\/|$)/u.test(url.pathname) };
  }
  if (host === "facebook.com") {
    return { id: host, isPrimaryChatRoute: /^\/messages(?:\/|$)/u.test(url.pathname) };
  }
  if (host === "messenger.com") {
    return { id: host, isPrimaryChatRoute: /^\/(?:e2ee\/)?t(?:\/|$)/u.test(url.pathname) };
  }
  return null;
}
