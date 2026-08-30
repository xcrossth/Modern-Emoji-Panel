import { describe, expect, it } from "vitest";
import {
  EMOJI_BASELINE_ID,
  EMOJI_SEQUENCE_COUNT,
  containsSupportedEmoji,
  isSupportedEmojiSequence,
  segmentText,
} from "../src/core/emoji-segmenter";

describe("Emoji Baseline grapheme detector", () => {
  it("uses the same pinned Emoji 17.0 baseline as Picker", () => {
    expect(EMOJI_BASELINE_ID).toBe("emoji-17.0_unicode-17.0.0_cldr-48.2_noto-v2.051");
    expect(EMOJI_SEQUENCE_COUNT).toBe(3944);
  });

  it.each([
    ["single code point", "🫩"],
    ["variation selector", "❤️"],
    ["skin tone", "👌🏻"],
    ["ZWJ", "👩🏽‍💻"],
    ["family", "👨‍👩‍👧‍👦"],
    ["keycap", "1️⃣"],
    ["regional flag", "🇹🇭"],
    ["tag sequence", "🏴󠁧󠁢󠁥󠁮󠁧󠁿"],
    ["Emoji 17.0", "🫯"],
  ])("recognizes %s as one supported grapheme", (_kind, emoji) => {
    expect(isSupportedEmojiSequence(emoji)).toBe(true);
    expect(segmentText(emoji)).toEqual([{ text: emoji, index: 0, isEmoji: true }]);
  });

  it("separates mixed Thai/English text without guessing plain symbols", () => {
    expect(segmentText("ไทย abc🫩def © 123")).toEqual(expect.arrayContaining([
      { text: "🫩", index: 7, isEmoji: true },
      { text: "©", index: 13, isEmoji: false },
    ]));
    expect(containsSupportedEmoji("ข้อความธรรมดา English 123")).toBe(false);
  });
});
