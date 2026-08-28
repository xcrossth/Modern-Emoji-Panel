using System.Globalization;
using System.Text.RegularExpressions;

namespace EmojiBaseline.Generator;

internal sealed class UnicodeEmojiParser
{
    private static readonly Regex EntryPattern = new(
        "^([0-9A-F ]+)\\s*;\\s*fully-qualified\\s*#\\s*\\S+\\s+E([0-9.]+)\\s+(.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public IReadOnlyList<ParsedEmoji> Parse(string emojiTestPath)
    {
        var entries = new List<ParsedEmoji>();
        var canonicalSequences = new HashSet<string>(StringComparer.Ordinal);
        var stableIds = new HashSet<string>(StringComparer.Ordinal);
        var group = string.Empty;
        var subgroup = string.Empty;

        foreach (var line in File.ReadLines(emojiTestPath))
        {
            if (line.StartsWith("# group: ", StringComparison.Ordinal))
            {
                group = line[9..].Trim();
                continue;
            }

            if (line.StartsWith("# subgroup: ", StringComparison.Ordinal))
            {
                subgroup = line[12..].Trim();
                continue;
            }

            var match = EntryPattern.Match(line);
            if (!match.Success)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(group) || string.IsNullOrWhiteSpace(subgroup))
            {
                throw new InvalidDataException("A fully-qualified emoji is missing its Unicode group or subgroup");
            }

            var codePoints = match.Groups[1].Value
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                .ToArray();
            var canonicalSequence = string.Join(' ', codePoints.Select(BaselineUtilities.FormatCodePoint));
            var sequenceAlias = BaselineUtilities.SequenceKey(codePoints, omitEmojiVariationSelector: false);
            var stableId = $"emoji-{sequenceAlias.Replace('_', '-')}";

            if (!canonicalSequences.Add(canonicalSequence))
            {
                throw new InvalidDataException($"Duplicate fully-qualified sequence: {canonicalSequence}");
            }

            if (!stableIds.Add(stableId))
            {
                throw new InvalidDataException($"Duplicate stable identifier: {stableId}");
            }

            entries.Add(new ParsedEmoji(
                entries.Count,
                stableId,
                string.Concat(codePoints.Select(char.ConvertFromUtf32)),
                codePoints,
                canonicalSequence,
                sequenceAlias,
                BaselineUtilities.SequenceKey(codePoints, omitEmojiVariationSelector: true),
                group,
                subgroup,
                match.Groups[2].Value,
                match.Groups[3].Value.Trim()));
        }

        if (entries.Count != 3944)
        {
            throw new InvalidDataException($"Emoji 17 fully-qualified count must be 3944, got {entries.Count}");
        }

        return entries;
    }
}
