import emojiData from "../generated/emoji-sequences.json";

export interface TextSegment {
  readonly text: string;
  readonly index: number;
  readonly isEmoji: boolean;
}

export const EMOJI_BASELINE_ID = emojiData.baselineId;
export const EMOJI_SEQUENCE_COUNT = emojiData.sequenceCount;

const supportedSequences: ReadonlySet<string> = new Set(emojiData.sequences);
const graphemeSegmenter = new Intl.Segmenter("und", { granularity: "grapheme" });

export function segmentText(text: string): readonly TextSegment[] {
  return Array.from(graphemeSegmenter.segment(text), part => ({
    text: part.segment,
    index: part.index,
    isEmoji: supportedSequences.has(part.segment),
  }));
}

export function containsSupportedEmoji(text: string): boolean {
  for (const part of graphemeSegmenter.segment(text)) {
    if (supportedSequences.has(part.segment)) return true;
  }
  return false;
}

export function isSupportedEmojiSequence(text: string): boolean {
  return supportedSequences.has(text);
}
