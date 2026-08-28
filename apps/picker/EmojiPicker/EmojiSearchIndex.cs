using System.Globalization;
using System.Text;

namespace EmojiPicker;

internal enum EmojiMatchTier
{
    ExactShortName = 0,
    ShortNameTermPrefix = 1,
    Keyword = 2,
    Substring = 3,
}

internal readonly record struct EmojiSearchMatch(Emoji Emoji, EmojiMatchTier Tier);

/// <summary>
/// Immutable, locale-independent index over the English and Thai CLDR metadata.
/// Match quality is always the primary sort key. Future learned ranking may add
/// a key inside a tier, but must leave <see cref="EmojiMatchTier"/> precedence intact.
/// </summary>
internal sealed class EmojiSearchIndex
{
    private readonly IReadOnlyList<IndexedEmoji> entries;
    private readonly Func<IReadOnlyDictionary<string, double>> learnedScores;

    public EmojiSearchIndex(
        IEnumerable<Emoji> emojis,
        Func<IReadOnlyDictionary<string, double>>? learnedScores = null)
    {
        this.learnedScores = learnedScores ?? (() => new Dictionary<string, double>());
        entries = emojis
            .Select(emoji => new IndexedEmoji(
                emoji,
                [Normalize(emoji.EnglishName), Normalize(emoji.ThaiName)],
                emoji.EnglishKeywords.Concat(emoji.ThaiKeywords)
                    .Select(Normalize)
                    .Where(keyword => keyword.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();
    }

    public IReadOnlyList<EmojiSearchMatch> Search(string query)
    {
        var normalizedQuery = Normalize(query);
        if (normalizedQuery.Length == 0)
        {
            return [];
        }

        var scoreSnapshot = learnedScores();
        return entries
            .Select(entry => Classify(entry, normalizedQuery))
            .Where(match => match.HasValue)
            .Select(match => match!.Value)
            .OrderBy(match => match.Tier)
            // Preference can reorder only peers in the same match tier. The
            // baseline CLDR order remains the deterministic final tie-breaker.
            .ThenByDescending(match => scoreSnapshot.GetValueOrDefault(match.Emoji.Id))
            .ThenBy(match => match.Emoji.Order)
            .ThenBy(match => match.Emoji.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static EmojiSearchMatch? Classify(IndexedEmoji entry, string query)
    {
        if (entry.ShortNames.Any(name => string.Equals(name, query, StringComparison.Ordinal)))
        {
            return new EmojiSearchMatch(entry.Emoji, EmojiMatchTier.ExactShortName);
        }

        if (entry.ShortNames.Any(name => HasTermPrefix(name, query)))
        {
            return new EmojiSearchMatch(entry.Emoji, EmojiMatchTier.ShortNameTermPrefix);
        }

        // A CLDR keyword is a search term, so an exact match or a prefix typed
        // interactively belongs to the keyword tier. Mid-term matches remain
        // substring matches and cannot outrank a better name/keyword result.
        if (entry.Keywords.Any(keyword => keyword.StartsWith(query, StringComparison.Ordinal)))
        {
            return new EmojiSearchMatch(entry.Emoji, EmojiMatchTier.Keyword);
        }

        if (entry.ShortNames.Any(name => name.Contains(query, StringComparison.Ordinal)) ||
            entry.Keywords.Any(keyword => keyword.Contains(query, StringComparison.Ordinal)))
        {
            return new EmojiSearchMatch(entry.Emoji, EmojiMatchTier.Substring);
        }

        return null;
    }

    internal static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);
        var pendingSpace = false;
        foreach (var character in normalized)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static bool HasTermPrefix(string text, string query)
    {
        var index = text.IndexOf(query, StringComparison.Ordinal);
        while (index >= 0)
        {
            if (index == 0 || !IsTermCharacter(text[index - 1]))
            {
                return true;
            }

            index = text.IndexOf(query, index + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool IsTermCharacter(char character)
    {
        var category = char.GetUnicodeCategory(character);
        return char.IsLetterOrDigit(character) ||
            category is UnicodeCategory.NonSpacingMark or
                UnicodeCategory.SpacingCombiningMark or
                UnicodeCategory.EnclosingMark;
    }

    private sealed record IndexedEmoji(Emoji Emoji, string[] ShortNames, string[] Keywords);
}
