using System.Text.Json;

namespace EmojiBaseline.Generator;

internal sealed class BaselineGenerator
{
    private const string BaselineId = "emoji-17.0_unicode-17.0.0_cldr-48.2_noto-v2.051";

    private readonly SourceLockVerifier sourceLockVerifier = new();
    private readonly UnicodeEmojiParser unicodeEmojiParser = new();
    private readonly NotoAssetCatalog notoAssetCatalog = new();

    public GenerationSummary Generate(GeneratorOptions options)
    {
        var sourceLock = sourceLockVerifier.Verify(options.RepositoryRoot);
        var emojiTestSource = sourceLock.GetSource("unicode-emoji-test");
        var emojiTestPath = BaselineUtilities.RepositoryPath(
            options.RepositoryRoot,
            emojiTestSource.Destination
                ?? throw new InvalidDataException("emoji-test destination is missing"));
        var parsedEmoji = unicodeEmojiParser.Parse(emojiTestPath);
        var cldr = CldrAnnotationCatalog.Load(options.RepositoryRoot, sourceLock);
        var assetCatalog = notoAssetCatalog.Build(options.RepositoryRoot, sourceLock, parsedEmoji);

        var entries = parsedEmoji.Select(emoji => new EmojiEntry(
            emoji.StableId,
            emoji.Order,
            emoji.Text,
            emoji.CodePoints.Select(BaselineUtilities.FormatCodePoint).ToArray(),
            emoji.CanonicalSequence,
            "fully-qualified",
            emoji.Group,
            emoji.Subgroup,
            emoji.EmojiVersion,
            cldr.GetEnglish(emoji),
            cldr.GetThai(emoji),
            assetCatalog.MappingByStableId[emoji.StableId])).ToArray();

        VerifyEntries(entries);
        Directory.CreateDirectory(options.OutputDirectory);

        var emojiData = new EmojiDataDocument(1, BaselineId, entries);
        var emojiDataPath = Path.Combine(options.OutputDirectory, "emoji.json");
        BaselineUtilities.WriteDeterministicJson(emojiDataPath, emojiData);

        var delta = Compare(options.PreviousEmojiDataPath, entries);
        var reviewReport = new ReviewReport(
            1,
            BaselineId,
            entries.Length,
            entries.Select(entry => entry.Group).Distinct(StringComparer.Ordinal).Count(),
            entries.Select(entry => $"{entry.Group}/{entry.Subgroup}").Distinct(StringComparer.Ordinal).Count(),
            entries.Count(entry => entry.Asset.SharedSourceForSizes),
            delta,
            assetCatalog.Anomalies);
        var reviewReportPath = Path.Combine(options.OutputDirectory, "review-report.json");
        BaselineUtilities.WriteDeterministicJson(reviewReportPath, reviewReport);

        var sourceManifest = new GeneratedSourceManifest(
            1,
            BaselineId,
            sourceLock.Document.Baseline,
            new GeneratedFileRecord(
                sourceLock.ManifestRelativePath,
                sourceLock.ManifestSha256,
                new FileInfo(BaselineUtilities.RepositoryPath(
                    options.RepositoryRoot,
                    sourceLock.ManifestRelativePath)).Length),
            sourceLock.Document.Sources.Select(source => new SourceManifestEntry(
                source.Id,
                source.SourceName,
                source.Version,
                source.ImmutableUrl,
                source.Commit,
                source.UpstreamCommit,
                source.Tree,
                source.Sha256,
                source.ByteLength,
                source.LicenseClass)).ToArray(),
            new[]
            {
                GeneratedFile(options.OutputDirectory, emojiDataPath),
                GeneratedFile(options.OutputDirectory, reviewReportPath),
            }.OrderBy(file => file.Path, StringComparer.Ordinal).ToArray());
        var sourceManifestPath = Path.Combine(options.OutputDirectory, "source-manifest.json");
        BaselineUtilities.WriteDeterministicJson(sourceManifestPath, sourceManifest);

        return new GenerationSummary(
            BaselineId,
            entries.Length,
            reviewReport.SharedFlagSourceCount,
            reviewReport.AssetAnomalies.AliasCollisions.Count,
            reviewReport.AssetAnomalies.AsymmetricAssets.Count,
            reviewReport.AssetAnomalies.UnreferencedAssets.Count,
            Path.GetFullPath(options.OutputDirectory));
    }

    private static GeneratedFileRecord GeneratedFile(string outputDirectory, string path) =>
        new(
            Path.GetRelativePath(outputDirectory, path).Replace(Path.DirectorySeparatorChar, '/'),
            BaselineUtilities.Sha256File(path),
            new FileInfo(path).Length);

    private static BaselineDelta Compare(string? previousEmojiDataPath, IReadOnlyList<EmojiEntry> currentEntries)
    {
        IReadOnlyList<EmojiEntry> previousEntries;
        string comparison;
        if (string.IsNullOrWhiteSpace(previousEmojiDataPath))
        {
            previousEntries = [];
            comparison = "empty-baseline";
        }
        else
        {
            var resolvedPath = Path.GetFullPath(previousEmojiDataPath);
            if (Directory.Exists(resolvedPath))
            {
                resolvedPath = Path.Combine(resolvedPath, "emoji.json");
            }

            if (!File.Exists(resolvedPath))
            {
                throw new InvalidDataException($"Previous Emoji Baseline is missing: {resolvedPath}");
            }

            previousEntries = BaselineUtilities.ReadJson<EmojiDataDocument>(resolvedPath).Entries;
            comparison = $"sha256:{BaselineUtilities.Sha256File(resolvedPath)}";
        }

        var previous = previousEntries.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        var current = currentEntries.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        var added = current.Keys.Except(previous.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var removed = previous.Keys.Except(current.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var changed = current.Keys
            .Intersect(previous.Keys, StringComparer.Ordinal)
            .Where(id => !EntryBytesEqual(previous[id], current[id]))
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new BaselineDelta(comparison, added, removed, changed);
    }

    private static bool EntryBytesEqual(EmojiEntry left, EmojiEntry right)
    {
        return string.Equals(
            JsonSerializer.Serialize(left, BaselineUtilities.JsonOptions),
            JsonSerializer.Serialize(right, BaselineUtilities.JsonOptions),
            StringComparison.Ordinal);
    }

    private static void VerifyEntries(IReadOnlyList<EmojiEntry> entries)
    {
        if (entries.Count != 3944)
        {
            throw new InvalidDataException($"Generated entry count must be 3944, got {entries.Count}");
        }

        if (entries.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count() != entries.Count ||
            entries.Select(entry => entry.CanonicalSequence).Distinct(StringComparer.Ordinal).Count() != entries.Count)
        {
            throw new InvalidDataException("Generated stable identifiers or canonical sequences are not unique");
        }

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (entry.Order != index ||
                string.IsNullOrWhiteSpace(entry.Group) ||
                string.IsNullOrWhiteSpace(entry.Subgroup) ||
                string.IsNullOrWhiteSpace(entry.EmojiVersion) ||
                string.IsNullOrWhiteSpace(entry.English.ShortName) ||
                string.IsNullOrWhiteSpace(entry.Thai.ShortName) ||
                entry.English.Keywords.Count == 0 ||
                entry.Thai.Keywords.Count == 0 ||
                string.IsNullOrWhiteSpace(entry.Asset.Png128) ||
                string.IsNullOrWhiteSpace(entry.Asset.Png512))
            {
                throw new InvalidDataException($"Generated Emoji Entry is incomplete: {entry.Id}");
            }
        }
    }
}

internal sealed record GenerationSummary(
    string BaselineId,
    int EntryCount,
    int SharedFlagSourceCount,
    int AliasCollisionCount,
    int AsymmetricAssetCount,
    int UnreferencedAssetCount,
    string OutputDirectory);
