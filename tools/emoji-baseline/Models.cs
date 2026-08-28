using System.Text.Json.Serialization;

namespace EmojiBaseline.Generator;

internal sealed class SourceLockDocument
{
    public int SchemaVersion { get; init; }

    public required BaselineVersions Baseline { get; init; }

    public required List<SourceLockEntry> Sources { get; init; }
}

internal sealed class BaselineVersions
{
    public required string Unicode { get; init; }

    public required string Emoji { get; init; }

    public required string Cldr { get; init; }

    public required string NotoEmoji { get; init; }

    public required string NotoCommit { get; init; }
}

internal sealed class SourceLockEntry
{
    public required string Id { get; init; }

    public required string Kind { get; init; }

    public required string SourceName { get; init; }

    public required string Version { get; init; }

    public required string ImmutableUrl { get; init; }

    public string? RepositoryUrl { get; init; }

    public string? Commit { get; init; }

    public string? UpstreamCommit { get; init; }

    public string? Tree { get; init; }

    public required string Sha256 { get; init; }

    public long ByteLength { get; init; }

    public long? FileCount { get; init; }

    public required string LicenseClass { get; init; }

    public string? Destination { get; init; }

    public string? Inventory { get; init; }

    public string? DestinationRoot { get; init; }

    public List<string>? SparsePaths { get; init; }
}

internal sealed record InventoryRecord(string Sha256, long ByteLength, string RelativePath);

internal sealed record VerifiedSourceLock(
    SourceLockDocument Document,
    string ManifestRelativePath,
    string ManifestSha256,
    IReadOnlyDictionary<string, IReadOnlyList<InventoryRecord>> Inventories)
{
    public SourceLockEntry GetSource(string id) =>
        Document.Sources.Single(source => string.Equals(source.Id, id, StringComparison.Ordinal));
}

internal sealed record ParsedEmoji(
    int Order,
    string StableId,
    string Text,
    IReadOnlyList<int> CodePoints,
    string CanonicalSequence,
    string SequenceAlias,
    string AssetLookupKey,
    string Group,
    string Subgroup,
    string EmojiVersion,
    string UnicodeName);

internal sealed record LocalizedMetadata(string ShortName, IReadOnlyList<string> Keywords);

internal sealed record AssetMapping(
    string Key,
    IReadOnlyList<string> Aliases,
    string Png128,
    string Png512,
    string SourceKind,
    bool SharedSourceForSizes);

internal sealed record EmojiEntry(
    string Id,
    int Order,
    string Text,
    IReadOnlyList<string> CodePoints,
    string CanonicalSequence,
    string Qualification,
    string Group,
    string Subgroup,
    string EmojiVersion,
    LocalizedMetadata English,
    LocalizedMetadata Thai,
    AssetMapping Asset);

internal sealed record EmojiDataDocument(
    int SchemaVersion,
    string BaselineId,
    IReadOnlyList<EmojiEntry> Entries);

internal sealed record AssetAnomalyReport(
    IReadOnlyList<string> AliasCollisions,
    IReadOnlyList<string> AsymmetricAssets,
    IReadOnlyList<string> UnreferencedAssets);

internal sealed record AssetCatalogResult(
    IReadOnlyDictionary<string, AssetMapping> MappingByStableId,
    AssetAnomalyReport Anomalies);

internal sealed record BaselineDelta(
    string Comparison,
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Removed,
    IReadOnlyList<string> Changed);

internal sealed record ReviewReport(
    int SchemaVersion,
    string BaselineId,
    int EntryCount,
    int GroupCount,
    int SubgroupCount,
    int SharedFlagSourceCount,
    BaselineDelta Delta,
    AssetAnomalyReport AssetAnomalies);

internal sealed record GeneratedFileRecord(string Path, string Sha256, long ByteLength);

internal sealed record SourceManifestEntry(
    string Id,
    string SourceName,
    string Version,
    string ImmutableUrl,
    string? Commit,
    string? UpstreamCommit,
    string? Tree,
    string Sha256,
    long ByteLength,
    string LicenseClass);

internal sealed record GeneratedSourceManifest(
    int SchemaVersion,
    string BaselineId,
    BaselineVersions Baseline,
    GeneratedFileRecord SourceLock,
    IReadOnlyList<SourceManifestEntry> Sources,
    IReadOnlyList<GeneratedFileRecord> GeneratedFiles);

internal sealed class GeneratorOptions
{
    public required string RepositoryRoot { get; init; }

    public required string OutputDirectory { get; init; }

    public string? PreviousEmojiDataPath { get; init; }
}
