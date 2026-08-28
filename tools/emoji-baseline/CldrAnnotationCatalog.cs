using System.Text.Json;

namespace EmojiBaseline.Generator;

internal sealed class CldrAnnotationCatalog
{
    private sealed class MutableMetadata
    {
        public SortedSet<string> ShortNames { get; } = new(StringComparer.Ordinal);

        public SortedSet<string> Keywords { get; } = new(StringComparer.Ordinal);
    }

    private readonly Dictionary<string, MutableMetadata> english = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MutableMetadata> thai = new(StringComparer.Ordinal);

    public static CldrAnnotationCatalog Load(string repositoryRoot, VerifiedSourceLock sourceLock)
    {
        var catalog = new CldrAnnotationCatalog();
        catalog.LoadLocale(repositoryRoot, sourceLock, "en", catalog.english);
        catalog.LoadLocale(repositoryRoot, sourceLock, "th", catalog.thai);
        return catalog;
    }

    public LocalizedMetadata GetEnglish(ParsedEmoji emoji) => Get(english, emoji, "English");

    public LocalizedMetadata GetThai(ParsedEmoji emoji) => Get(thai, emoji, "Thai");

    private static LocalizedMetadata Get(
        IReadOnlyDictionary<string, MutableMetadata> metadataByKey,
        ParsedEmoji emoji,
        string localeName)
    {
        if (!metadataByKey.TryGetValue(emoji.AssetLookupKey, out var metadata) ||
            metadata.ShortNames.Count == 0 ||
            metadata.Keywords.Count == 0)
        {
            throw new InvalidDataException(
                $"{localeName} CLDR metadata is incomplete for {emoji.StableId} ({emoji.CanonicalSequence})");
        }

        return new LocalizedMetadata(metadata.ShortNames.First(), metadata.Keywords.ToArray());
    }

    private void LoadLocale(
        string repositoryRoot,
        VerifiedSourceLock sourceLock,
        string locale,
        Dictionary<string, MutableMetadata> destination)
    {
        LoadFile(repositoryRoot, sourceLock.GetSource($"cldr-annotations-{locale}"), "annotations", destination);
        LoadFile(
            repositoryRoot,
            sourceLock.GetSource($"cldr-annotations-derived-{locale}"),
            "annotationsDerived",
            destination);
    }

    private static void LoadFile(
        string repositoryRoot,
        SourceLockEntry source,
        string rootProperty,
        Dictionary<string, MutableMetadata> destination)
    {
        if (string.IsNullOrWhiteSpace(source.Destination))
        {
            throw new InvalidDataException($"CLDR destination is missing for {source.Id}");
        }

        using var document = JsonDocument.Parse(File.ReadAllBytes(
            BaselineUtilities.RepositoryPath(repositoryRoot, source.Destination)));
        var annotations = document.RootElement
            .GetProperty(rootProperty)
            .GetProperty("annotations");

        foreach (var property in annotations.EnumerateObject())
        {
            var key = BaselineUtilities.TextKey(property.Name, omitEmojiVariationSelector: true);
            if (!destination.TryGetValue(key, out var metadata))
            {
                metadata = new MutableMetadata();
                destination.Add(key, metadata);
            }

            if (property.Value.TryGetProperty("tts", out var shortNames))
            {
                foreach (var shortName in shortNames.EnumerateArray())
                {
                    if (!string.IsNullOrWhiteSpace(shortName.GetString()))
                    {
                        metadata.ShortNames.Add(shortName.GetString()!);
                    }
                }
            }

            if (property.Value.TryGetProperty("default", out var keywords))
            {
                foreach (var keyword in keywords.EnumerateArray())
                {
                    if (!string.IsNullOrWhiteSpace(keyword.GetString()))
                    {
                        metadata.Keywords.Add(keyword.GetString()!);
                    }
                }
            }
        }
    }
}
