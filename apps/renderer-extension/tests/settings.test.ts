import { describe, expect, it } from "vitest";
import {
  DEFAULT_SETTINGS,
  SETTINGS_SCHEMA_VERSION,
  isSiteEnabled,
  migrateSettings,
  normalizeSite,
  settingsEqual,
  withSiteEnabled,
} from "../src/settings/settings";
import { buildRegistrationPolicy, requiredOptionalOrigins } from "../src/settings/permissions";

describe("renderer settings", () => {
  it("defaults to the four supported chat sites with diagnostics off", () => {
    expect(DEFAULT_SETTINGS).toEqual({
      schemaVersion: 2,
      enabled: true,
      mode: "allowlist",
      sites: ["instagram.com", "tiktok.com", "facebook.com", "messenger.com"],
      rendererMode: "noto-colrv1",
      processDynamicContent: true,
      debug: false,
    });
    expect(isSiteEnabled(DEFAULT_SETTINGS, "www.instagram.com")).toBe(true);
    expect(isSiteEnabled(DEFAULT_SETTINGS, "www.facebook.com")).toBe(true);
    expect(isSiteEnabled(DEFAULT_SETTINGS, "www.messenger.com")).toBe(true);
    expect(isSiteEnabled(DEFAULT_SETTINGS, "example.com")).toBe(false);
  });

  it("adds Facebook and Messenger when migrating untouched version 1 defaults", () => {
    const migrated = migrateSettings({
      schemaVersion: 1,
      enabled: true,
      mode: "allowlist",
      sites: ["instagram.com", "tiktok.com"],
      rendererMode: "noto-colrv1",
      processDynamicContent: true,
      debug: false,
    });

    expect(migrated.sites).toEqual([
      "instagram.com", "tiktok.com", "facebook.com", "messenger.com",
    ]);
  });

  it("migrates legacy and malformed values into the current schema deterministically", () => {
    const migrated = migrateSettings({
      enabled: false,
      mode: "allowlist",
      sites: ["WWW.TIKTOK.COM", "https://instagram.com/direct/", "bad site", "tiktok.com"],
      emojiStyle: "noto",
      debug: true,
    });
    expect(migrated.schemaVersion).toBe(SETTINGS_SCHEMA_VERSION);
    expect(migrated.sites).toEqual([
      "instagram.com", "tiktok.com", "facebook.com", "messenger.com",
    ]);
    expect(migrated.rendererMode).toBe("noto-colrv1");
    expect(migrated.enabled).toBe(false);
    expect(migrated.debug).toBe(true);
    expect(settingsEqual(migrateSettings(migrated), migrated)).toBe(true);
  });

  it.each([
    ["example.com", "example.com"],
    ["www.example.com", "example.com"],
    ["https://www.example.com/path", "example.com"],
    ["*.example.com", "example.com"],
    ["bad site", null],
    ["localhost", null],
    ["chrome://extensions", null],
    ["user@example.com", null],
  ])("normalizes site input %s", (input, expected) => {
    expect(normalizeSite(input)).toBe(expected);
  });

  it("applies per-site toggles consistently in allowlist, denylist and all modes", () => {
    const added = withSiteEnabled(DEFAULT_SETTINGS, "example.com", true);
    expect(isSiteEnabled(added, "example.com")).toBe(true);
    expect(isSiteEnabled(withSiteEnabled(added, "example.com", false), "example.com")).toBe(false);

    const denylist = { ...DEFAULT_SETTINGS, mode: "denylist" as const, sites: ["example.com"] };
    expect(isSiteEnabled(denylist, "example.com")).toBe(false);
    expect(isSiteEnabled(withSiteEnabled(denylist, "example.com", true), "example.com")).toBe(true);

    const all = { ...DEFAULT_SETTINGS, mode: "all" as const, sites: [] };
    const disabled = withSiteEnabled(all, "example.com", false);
    expect(disabled.mode).toBe("denylist");
    expect(isSiteEnabled(disabled, "example.com")).toBe(false);
  });

  it("requests broad permission only for all/denylist and narrows custom allowlists", () => {
    expect(requiredOptionalOrigins(DEFAULT_SETTINGS)).toEqual([]);
    const custom = withSiteEnabled(DEFAULT_SETTINGS, "example.com", true);
    expect(requiredOptionalOrigins(custom)).toEqual(["*://*.example.com/*"]);
    expect(buildRegistrationPolicy(custom)).toEqual({
      matches: ["*://*.example.com/*"], excludeMatches: [],
    });
    const all = { ...DEFAULT_SETTINGS, mode: "all" as const, sites: [] };
    expect(requiredOptionalOrigins(all)).toEqual(["<all_urls>"]);
    expect(buildRegistrationPolicy(all)).toEqual({
      matches: ["<all_urls>"],
      excludeMatches: [
        "https://www.instagram.com/*",
        "https://www.tiktok.com/*",
        "https://www.facebook.com/*",
        "https://www.messenger.com/*",
      ],
    });
    const denied = { ...DEFAULT_SETTINGS, mode: "denylist" as const, sites: ["example.com"] };
    expect(buildRegistrationPolicy(denied)?.excludeMatches).toContain("*://*.example.com/*");
  });
});
