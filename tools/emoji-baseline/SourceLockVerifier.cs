using System.Text.Json;
using System.Text.RegularExpressions;

namespace EmojiBaseline.Generator;

internal sealed class SourceLockVerifier
{
    private static readonly Regex InventoryLinePattern = new(
        "^([a-f0-9]{64})\\t([0-9]+)\\t(.+)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MovingReferencePattern = new(
        "(^|[/_.-])(latest|draft|beta|main|master)([/_.-]|$)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] RequiredSourceIds =
    [
        "unicode-emoji-test",
        "unicode-emoji-sequences",
        "unicode-emoji-zwj-sequences",
        "unicode-emoji-data",
        "unicode-emoji-variation-sequences",
        "unicode-grapheme-break-property",
        "unicode-grapheme-break-test",
        "cldr-annotations-en",
        "cldr-annotations-th",
        "cldr-annotations-derived-en",
        "cldr-annotations-derived-th",
        "noto-canonical-png",
        "noto-region-flags",
    ];

    public VerifiedSourceLock Verify(string repositoryRoot)
    {
        var manifestRelativePath = "vendor/emoji-baseline/sources.lock.json";
        var manifestPath = BaselineUtilities.RepositoryPath(repositoryRoot, manifestRelativePath);
        if (!File.Exists(manifestPath))
        {
            throw new InvalidDataException("Emoji Baseline source lock is missing");
        }

        var document = JsonSerializer.Deserialize<SourceLockDocument>(
            File.ReadAllText(manifestPath),
            BaselineUtilities.JsonOptions)
            ?? throw new InvalidDataException("Emoji Baseline source lock is invalid");

        VerifyManifestShape(document);
        var inventories = new Dictionary<string, IReadOnlyList<InventoryRecord>>(StringComparer.Ordinal);

        foreach (var source in document.Sources)
        {
            VerifySourceMetadata(source);
            if (string.Equals(source.Kind, "file", StringComparison.Ordinal))
            {
                VerifyFileSource(repositoryRoot, source);
                continue;
            }

            if (!string.Equals(source.Kind, "git-inventory", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Unknown source kind for {source.Id}: {source.Kind}");
            }

            inventories.Add(source.Id, VerifyInventorySource(repositoryRoot, source));
        }

        return new VerifiedSourceLock(
            document,
            manifestRelativePath,
            BaselineUtilities.Sha256File(manifestPath),
            inventories);
    }

    private static void VerifyManifestShape(SourceLockDocument document)
    {
        if (document.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Unsupported source-lock schema: {document.SchemaVersion}");
        }

        if (document.Baseline.Unicode != "17.0.0" ||
            document.Baseline.Emoji != "17.0" ||
            document.Baseline.Cldr != "48.2" ||
            document.Baseline.NotoEmoji != "v2.051" ||
            document.Baseline.NotoCommit != "8998f5dd683424a73e2314a8c1f1e359c19e8742")
        {
            throw new InvalidDataException("Source lock does not match the approved Emoji Baseline");
        }

        var duplicateIds = document.Sources
            .GroupBy(source => source.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateIds.Length != 0)
        {
            throw new InvalidDataException($"Duplicate source IDs: {string.Join(", ", duplicateIds)}");
        }

        var sourceIds = document.Sources.Select(source => source.Id).ToHashSet(StringComparer.Ordinal);
        var missingIds = RequiredSourceIds.Where(id => !sourceIds.Contains(id)).ToArray();
        if (missingIds.Length != 0)
        {
            throw new InvalidDataException($"Required sources are missing: {string.Join(", ", missingIds)}");
        }
    }

    private static void VerifySourceMetadata(SourceLockEntry source)
    {
        if (string.IsNullOrWhiteSpace(source.Id) ||
            string.IsNullOrWhiteSpace(source.SourceName) ||
            string.IsNullOrWhiteSpace(source.Version) ||
            string.IsNullOrWhiteSpace(source.LicenseClass))
        {
            throw new InvalidDataException("A source-lock entry has incomplete metadata");
        }

        if (!Uri.TryCreate(source.ImmutableUrl, UriKind.Absolute, out _) ||
            MovingReferencePattern.IsMatch(source.ImmutableUrl))
        {
            throw new InvalidDataException($"Source URL is not immutable for {source.Id}: {source.ImmutableUrl}");
        }

        if (!Regex.IsMatch(source.Sha256, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant) ||
            source.ByteLength < 0)
        {
            throw new InvalidDataException($"Source checksum metadata is invalid for {source.Id}");
        }
    }

    private static void VerifyFileSource(string repositoryRoot, SourceLockEntry source)
    {
        if (string.IsNullOrWhiteSpace(source.Destination))
        {
            throw new InvalidDataException($"File destination is missing for {source.Id}");
        }

        VerifyFile(
            BaselineUtilities.RepositoryPath(repositoryRoot, source.Destination),
            source.ByteLength,
            source.Sha256,
            source.Id);
    }

    private static IReadOnlyList<InventoryRecord> VerifyInventorySource(
        string repositoryRoot,
        SourceLockEntry source)
    {
        if (string.IsNullOrWhiteSpace(source.Inventory) ||
            string.IsNullOrWhiteSpace(source.DestinationRoot) ||
            source.FileCount is null)
        {
            throw new InvalidDataException($"Git inventory metadata is incomplete for {source.Id}");
        }

        if (string.IsNullOrWhiteSpace(source.Commit) ||
            string.IsNullOrWhiteSpace(source.Tree) ||
            !Regex.IsMatch(source.Commit, "^[a-f0-9]{40}$", RegexOptions.CultureInvariant) ||
            !Regex.IsMatch(source.Tree, "^[a-f0-9]{40}$", RegexOptions.CultureInvariant) ||
            !source.ImmutableUrl.Contains(source.Commit, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Pinned Git identity is invalid for {source.Id}");
        }

        var inventoryPath = BaselineUtilities.RepositoryPath(repositoryRoot, source.Inventory);
        VerifyFile(inventoryPath, new FileInfo(inventoryPath).Length, source.Sha256, $"{source.Id} inventory");

        var records = new List<InventoryRecord>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long aggregateLength = 0;
        var lineNumber = 0;
        foreach (var line in File.ReadLines(inventoryPath))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var match = InventoryLinePattern.Match(line);
            if (!match.Success || !long.TryParse(match.Groups[2].Value, out var byteLength))
            {
                throw new InvalidDataException($"Invalid inventory line for {source.Id} at {lineNumber}");
            }

            var relativePath = match.Groups[3].Value.Replace('\\', '/');
            if (Path.IsPathRooted(relativePath) || relativePath.Split('/').Contains("..", StringComparer.Ordinal))
            {
                throw new InvalidDataException($"Inventory path escapes its destination for {source.Id}: {relativePath}");
            }

            if (!paths.Add(relativePath))
            {
                throw new InvalidDataException($"Duplicate inventory path for {source.Id}: {relativePath}");
            }

            var repositoryRelativePath = $"{source.DestinationRoot.TrimEnd('/', '\\')}/{relativePath}";
            VerifyFile(
                BaselineUtilities.RepositoryPath(repositoryRoot, repositoryRelativePath),
                byteLength,
                match.Groups[1].Value,
                $"{source.Id}:{relativePath}");

            records.Add(new InventoryRecord(match.Groups[1].Value, byteLength, relativePath));
            aggregateLength += byteLength;
        }

        if (records.Count != source.FileCount.Value || aggregateLength != source.ByteLength)
        {
            throw new InvalidDataException(
                $"Inventory coverage differs for {source.Id}: {records.Count} files / {aggregateLength} bytes");
        }

        return records;
    }

    private static void VerifyFile(string path, long expectedLength, string expectedSha256, string sourceId)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"Source file is missing for {sourceId}: {path}");
        }

        var file = new FileInfo(path);
        if (file.Length != expectedLength)
        {
            throw new InvalidDataException($"Source byte length differs for {sourceId}");
        }

        var sha256 = BaselineUtilities.Sha256File(path);
        if (!string.Equals(sha256, expectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Source SHA-256 differs for {sourceId}");
        }
    }
}
