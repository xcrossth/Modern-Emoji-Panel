import { describe, expect, it } from "vitest";
import { DEFAULT_PRIMARY_SITES, identifyPrimarySite } from "../src/sites/site-context";

describe("primary site context", () => {
  it("recognizes Instagram DM without depending on message selectors", () => {
    expect(identifyPrimarySite(new URL("https://www.instagram.com/direct/t/123/"))).toEqual({
      id: "instagram.com", isPrimaryChatRoute: true,
    });
    expect(identifyPrimarySite(new URL("https://www.instagram.com/explore/"))).toEqual({
      id: "instagram.com", isPrimaryChatRoute: false,
    });
  });

  it("recognizes TikTok messages and keeps all primary sites in the default policy", () => {
    expect(identifyPrimarySite(new URL("https://www.tiktok.com/messages?lang=th"))).toEqual({
      id: "tiktok.com", isPrimaryChatRoute: true,
    });
    expect(identifyPrimarySite(new URL("https://www.facebook.com/messages/e2ee/t/123"))).toEqual({
      id: "facebook.com", isPrimaryChatRoute: true,
    });
    expect(identifyPrimarySite(new URL("https://www.facebook.com/"))).toEqual({
      id: "facebook.com", isPrimaryChatRoute: false,
    });
    expect(identifyPrimarySite(new URL("https://www.messenger.com/e2ee/t/123"))).toEqual({
      id: "messenger.com", isPrimaryChatRoute: true,
    });
    expect(DEFAULT_PRIMARY_SITES).toEqual([
      "instagram.com", "tiktok.com", "facebook.com", "messenger.com",
    ]);
    expect(identifyPrimarySite(new URL("https://example.com/messages"))).toBeNull();
  });
});
