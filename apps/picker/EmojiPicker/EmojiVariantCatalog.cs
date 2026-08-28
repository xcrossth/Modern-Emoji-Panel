using System.IO;
using System.Text.RegularExpressions;

namespace EmojiPicker;

internal enum SkinTonePreference
{
    Neutral,
    Light,
    MediumLight,
    Medium,
    MediumDark,
    Dark,
}

internal static class SkinTonePreferenceNames
{
    public static string ToSettingValue(this SkinTonePreference preference) => preference switch
    {
        SkinTonePreference.Light => "light",
        SkinTonePreference.MediumLight => "medium-light",
        SkinTonePreference.Medium => "medium",
        SkinTonePreference.MediumDark => "medium-dark",
        SkinTonePreference.Dark => "dark",
        _ => "neutral",
    };

    public static SkinTonePreference ParseSettingValue(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "light" => SkinTonePreference.Light,
        "medium-light" => SkinTonePreference.MediumLight,
        "medium" => SkinTonePreference.Medium,
        "medium-dark" => SkinTonePreference.MediumDark,
        "dark" => SkinTonePreference.Dark,
        _ => SkinTonePreference.Neutral,
    };

}

internal sealed record EmojiSelection(
    Emoji BaseEntry,
    Emoji ResolvedEntry,
    bool IsVariantOverride)
{
    public Emoji ToPresentation() => new(
        id: BaseEntry.Id,
        character: ResolvedEntry.Character,
        name: BaseEntry.Name,
        englishName: BaseEntry.EnglishName,
        thaiName: BaseEntry.ThaiName,
        category: BaseEntry.Category,
        canonicalSequence: ResolvedEntry.CanonicalSequence,
        keywords: BaseEntry.Keywords,
        emojiVersion: ResolvedEntry.EmojiVersion,
        assetPath: ResolvedEntry.AssetPath,
        order: BaseEntry.Order,
        popularity: BaseEntry.Popularity,
        baseCanonicalSequence: BaseEntry.CanonicalSequence,
        resolvedEntryId: ResolvedEntry.Id,
        isVariantOverride: IsVariantOverride);
}

/// <summary>
/// Resolves the complete skin-tone surface of an Emoji Baseline through one
/// small interface. Callers never construct Unicode sequences or infer Noto
/// filenames: every result is an existing fully-qualified baseline entry.
/// </summary>
internal sealed partial class EmojiVariantCatalog
{
    private static readonly HashSet<string> ModifierCodePoints =
    [
        "1F3FB",
        "1F3FC",
        "1F3FD",
        "1F3FE",
        "1F3FF",
    ];

    private readonly Dictionary<string, VariantFamily> familyByEntryId;
    private readonly Dictionary<string, Emoji> entryById;

    public EmojiVariantCatalog(IReadOnlyList<Emoji> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            throw new ArgumentException("Emoji Baseline must contain at least one entry.", nameof(entries));
        }

        entryById = entries.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        var neutralEntries = entries.Where(entry => GetModifiers(entry).Count == 0).ToList();
        BaseEntries = neutralEntries.OrderBy(entry => entry.Order).ToList();

        var neutralBySkeleton = neutralEntries.ToDictionary(
            entry => GetSkeleton(entry.CanonicalSequence),
            entry => entry,
            StringComparer.Ordinal);
        var neutralByName = neutralEntries
            .GroupBy(entry => NormalizeVariantName(entry.EnglishName), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);

        var builders = neutralEntries.ToDictionary(
            entry => entry.Id,
            entry => new VariantFamilyBuilder(entry),
            StringComparer.Ordinal);
        familyByEntryId = new Dictionary<string, VariantFamily>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            var modifiers = GetModifiers(entry);
            Emoji baseEntry;
            if (modifiers.Count == 0)
            {
                baseEntry = entry;
            }
            else if (!neutralBySkeleton.TryGetValue(GetSkeleton(entry.CanonicalSequence), out baseEntry!))
            {
                var normalizedName = NormalizeVariantName(entry.EnglishName);
                if (!neutralByName.TryGetValue(normalizedName, out baseEntry!) &&
                    !neutralByName.TryGetValue(RemoveGenericPairSuffix(normalizedName), out baseEntry!))
                {
                    throw new InvalidDataException(
                        $"Skin-tone entry '{entry.CanonicalSequence}' has no unambiguous neutral Emoji Entry.");
                }
            }

            builders[baseEntry.Id].Add(entry, modifiers);
        }

        foreach (var builder in builders.Values)
        {
            var family = builder.Build();
            foreach (var member in family.AllEntries)
            {
                if (!familyByEntryId.TryAdd(member.Id, family))
                {
                    throw new InvalidDataException($"Emoji Entry '{member.Id}' belongs to more than one variant family.");
                }
            }
        }

        if (familyByEntryId.Count != entries.Count)
        {
            throw new InvalidDataException(
                $"Variant catalog mapped {familyByEntryId.Count} of {entries.Count} Emoji Baseline entries.");
        }
    }

    public IReadOnlyList<Emoji> BaseEntries { get; }

    public EmojiSelection Resolve(
        Emoji entry,
        SkinTonePreference preference,
        string? variantOverrideEntryId = null)
    {
        var family = GetFamily(entry);
        if (!string.IsNullOrWhiteSpace(variantOverrideEntryId))
        {
            if (!entryById.TryGetValue(variantOverrideEntryId, out var requested) ||
                !family.MixedToneOverrides.Any(candidate => candidate.Id == requested.Id))
            {
                throw new ArgumentException(
                    "Variant Override must name a mixed-tone sequence in the selected Emoji Entry family.",
                    nameof(variantOverrideEntryId));
            }

            return new EmojiSelection(family.BaseEntry, requested, IsVariantOverride: true);
        }

        var resolved = preference == SkinTonePreference.Neutral
            ? family.BaseEntry
            : family.UniformToneEntries.GetValueOrDefault(preference, family.BaseEntry);
        return new EmojiSelection(family.BaseEntry, resolved, IsVariantOverride: false);
    }

    public EmojiSelection RestoreResolved(Emoji resolvedEntry)
    {
        var family = GetFamily(resolvedEntry);
        var isMixed = family.MixedToneOverrides.Any(candidate => candidate.Id == resolvedEntry.Id);
        return new EmojiSelection(family.BaseEntry, resolvedEntry, isMixed);
    }

    public IReadOnlyList<Emoji> GetVariantOverrides(Emoji entry) => GetFamily(entry).MixedToneOverrides;

    public bool SupportsSkinTone(Emoji entry)
    {
        var family = GetFamily(entry);
        return family.UniformToneEntries.Count > 0 || family.MixedToneOverrides.Count > 0;
    }

    private VariantFamily GetFamily(Emoji entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!familyByEntryId.TryGetValue(entry.Id, out var family))
        {
            throw new ArgumentException($"Emoji Entry '{entry.Id}' is outside this Emoji Baseline.", nameof(entry));
        }

        return family;
    }

    private static IReadOnlyList<string> GetModifiers(Emoji entry) => entry.CanonicalSequence
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Where(ModifierCodePoints.Contains)
        .ToList();

    private static string GetSkeleton(string canonicalSequence) => string.Join(
        ' ',
        canonicalSequence
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(codePoint => codePoint != "FE0F" && !ModifierCodePoints.Contains(codePoint)));

    private static string NormalizeVariantName(string name) =>
        SkinToneSuffixRegex().Replace(name, string.Empty).TrimEnd();

    private static string RemoveGenericPairSuffix(string name) => name.EndsWith(
        ": person, person",
        StringComparison.OrdinalIgnoreCase)
        ? name[..^": person, person".Length]
        : name;

    [GeneratedRegex(@"(?:: |, )(?:light|medium-light|medium|medium-dark|dark) skin tone(?:,.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex SkinToneSuffixRegex();

    private sealed class VariantFamilyBuilder(Emoji baseEntry)
    {
        private readonly Dictionary<SkinTonePreference, Emoji> uniform = [];
        private readonly List<Emoji> mixed = [];
        private readonly List<Emoji> all = [];

        public void Add(Emoji entry, IReadOnlyList<string> modifiers)
        {
            all.Add(entry);
            if (modifiers.Count == 0)
            {
                return;
            }

            var distinctModifiers = modifiers.Distinct(StringComparer.Ordinal).ToList();
            if (distinctModifiers.Count == 1)
            {
                var preference = PreferenceFromCodePoint(distinctModifiers[0]);
                if (!uniform.TryAdd(preference, entry))
                {
                    throw new InvalidDataException(
                        $"Emoji Entry '{baseEntry.Id}' has duplicate {preference} variants.");
                }
            }
            else
            {
                mixed.Add(entry);
            }
        }

        public VariantFamily Build() => new(
            baseEntry,
            uniform,
            mixed.OrderBy(entry => entry.Order).ToList(),
            all.OrderBy(entry => entry.Order).ToList());

        private static SkinTonePreference PreferenceFromCodePoint(string codePoint) => codePoint switch
        {
            "1F3FB" => SkinTonePreference.Light,
            "1F3FC" => SkinTonePreference.MediumLight,
            "1F3FD" => SkinTonePreference.Medium,
            "1F3FE" => SkinTonePreference.MediumDark,
            "1F3FF" => SkinTonePreference.Dark,
            _ => throw new InvalidDataException($"Unsupported skin-tone modifier '{codePoint}'."),
        };
    }

    private sealed record VariantFamily(
        Emoji BaseEntry,
        IReadOnlyDictionary<SkinTonePreference, Emoji> UniformToneEntries,
        IReadOnlyList<Emoji> MixedToneOverrides,
        IReadOnlyList<Emoji> AllEntries);
}
