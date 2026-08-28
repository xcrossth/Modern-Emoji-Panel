using System.Text;
using System.Text.RegularExpressions;

namespace EmojiBaseline.Generator;

internal sealed class NotoAssetCatalog
{
    private sealed record AssetCandidate(string RepositoryRelativePath, bool UsesCanonicalName);

    private static readonly Regex CanonicalPathPattern = new(
        "^png/(128|512)/emoji_u([0-9a-f_]+)\\.png$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex LegacyPathPattern = new(
        "^png/(128|512)/(u[0-9a-f]+(?:-u[0-9a-f]+)*)\\.png$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex RegionFlagPathPattern = new(
        "^third_party/region-flags/png/([^/]+)\\.png$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public AssetCatalogResult Build(
        string repositoryRoot,
        VerifiedSourceLock sourceLock,
        IReadOnlyList<ParsedEmoji> emoji)
    {
        var canonicalSource = sourceLock.GetSource("noto-canonical-png");
        var regionSource = sourceLock.GetSource("noto-region-flags");
        var bySize = new Dictionary<int, Dictionary<string, List<AssetCandidate>>>
        {
            [128] = new(StringComparer.Ordinal),
            [512] = new(StringComparer.Ordinal),
        };
        var allAssetPaths = new HashSet<string>(StringComparer.Ordinal);
        var selectedAssetPaths = new HashSet<string>(StringComparer.Ordinal);
        var aliasCollisions = new SortedSet<string>(StringComparer.Ordinal);
        var asymmetricAssets = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var record in sourceLock.Inventories[canonicalSource.Id])
        {
            var repositoryRelativePath = CombineRepositoryPath(canonicalSource.DestinationRoot!, record.RelativePath);
            allAssetPaths.Add(repositoryRelativePath);

            var canonicalMatch = CanonicalPathPattern.Match(record.RelativePath);
            if (canonicalMatch.Success)
            {
                AddCandidate(
                    bySize[int.Parse(canonicalMatch.Groups[1].Value)],
                    NormalizeFilenameKey(canonicalMatch.Groups[2].Value.Split('_')),
                    new AssetCandidate(repositoryRelativePath, UsesCanonicalName: true));
                continue;
            }

            var legacyMatch = LegacyPathPattern.Match(record.RelativePath);
            if (legacyMatch.Success)
            {
                AddCandidate(
                    bySize[int.Parse(legacyMatch.Groups[1].Value)],
                    NormalizeFilenameKey(
                        legacyMatch.Groups[2].Value.Split('-').Select(value => value[1..])),
                    new AssetCandidate(repositoryRelativePath, UsesCanonicalName: false));
                continue;
            }

            throw new InvalidDataException($"Unknown canonical Noto asset path: {record.RelativePath}");
        }

        foreach (var key in bySize[128].Keys.Union(bySize[512].Keys, StringComparer.Ordinal))
        {
            if (!bySize[128].ContainsKey(key) || !bySize[512].ContainsKey(key))
            {
                asymmetricAssets.Add(key);
            }
        }

        var regionFlags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in sourceLock.Inventories[regionSource.Id])
        {
            var match = RegionFlagPathPattern.Match(record.RelativePath);
            if (!match.Success)
            {
                throw new InvalidDataException($"Unknown Noto region-flag path: {record.RelativePath}");
            }

            var repositoryRelativePath = CombineRepositoryPath(regionSource.DestinationRoot!, record.RelativePath);
            allAssetPaths.Add(repositoryRelativePath);
            if (!regionFlags.TryAdd(match.Groups[1].Value, repositoryRelativePath))
            {
                throw new InvalidDataException($"Duplicate Noto region flag: {match.Groups[1].Value}");
            }
        }

        var mappings = new Dictionary<string, AssetMapping>(StringComparer.Ordinal);
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in emoji)
        {
            AssetMapping mapping;
            if (TryGetRegionFlagCode(entry.CodePoints, out var regionCode))
            {
                mapping = BuildRegionFlagMapping(entry, regionCode, regionFlags, selectedAssetPaths);
            }
            else
            {
                mapping = BuildCanonicalMapping(entry, bySize, selectedAssetPaths, aliasCollisions);
            }

            if (!mappings.TryAdd(entry.StableId, mapping))
            {
                throw new InvalidDataException($"Duplicate asset mapping for {entry.StableId}");
            }

            foreach (var alias in mapping.Aliases)
            {
                if (aliases.TryGetValue(alias, out var existingId) && existingId != entry.StableId)
                {
                    throw new InvalidDataException(
                        $"Noto alias is ambiguous: {alias} maps to {existingId} and {entry.StableId}");
                }

                aliases[alias] = entry.StableId;
            }

            VerifyMappedFile(repositoryRoot, mapping.Png128, entry.StableId, "128/grid");
            VerifyMappedFile(repositoryRoot, mapping.Png512, entry.StableId, "512/preview");
        }

        if (mappings.Count != emoji.Count)
        {
            throw new InvalidDataException($"Asset coverage is incomplete: {mappings.Count}/{emoji.Count}");
        }

        return new AssetCatalogResult(
            mappings,
            new AssetAnomalyReport(
                aliasCollisions.ToArray(),
                asymmetricAssets.ToArray(),
                allAssetPaths.Except(selectedAssetPaths, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()));
    }

    private static AssetMapping BuildCanonicalMapping(
        ParsedEmoji emoji,
        IReadOnlyDictionary<int, Dictionary<string, List<AssetCandidate>>> bySize,
        ISet<string> selectedAssetPaths,
        ISet<string> aliasCollisions)
    {
        var png128 = SelectCanonicalCandidate(emoji.AssetLookupKey, 128, bySize[128], aliasCollisions);
        var png512 = SelectCanonicalCandidate(emoji.AssetLookupKey, 512, bySize[512], aliasCollisions);
        selectedAssetPaths.Add(png128.RepositoryRelativePath);
        selectedAssetPaths.Add(png512.RepositoryRelativePath);

        return new AssetMapping(
            $"noto-{emoji.AssetLookupKey.Replace('_', '-')}",
            new[]
            {
                $"filename:emoji_u{emoji.AssetLookupKey}.png",
                $"noto:{emoji.AssetLookupKey}",
                $"sequence:{emoji.SequenceAlias}",
            }.Order(StringComparer.Ordinal).ToArray(),
            png128.RepositoryRelativePath,
            png512.RepositoryRelativePath,
            "noto-canonical",
            SharedSourceForSizes: false);
    }

    private static AssetMapping BuildRegionFlagMapping(
        ParsedEmoji emoji,
        string regionCode,
        IReadOnlyDictionary<string, string> regionFlags,
        ISet<string> selectedAssetPaths)
    {
        if (!regionFlags.TryGetValue(regionCode, out var path))
        {
            throw new InvalidDataException(
                $"Noto region flag is missing for {emoji.StableId}: expected {regionCode}.png");
        }

        selectedAssetPaths.Add(path);
        return new AssetMapping(
            $"region-flag-{regionCode.ToLowerInvariant()}",
            new[]
            {
                $"filename:{regionCode}.png",
                $"region:{regionCode}",
                $"sequence:{emoji.SequenceAlias}",
            }.Order(StringComparer.Ordinal).ToArray(),
            path,
            path,
            "noto-region-flag",
            SharedSourceForSizes: true);
    }

    private static AssetCandidate SelectCanonicalCandidate(
        string key,
        int size,
        IReadOnlyDictionary<string, List<AssetCandidate>> candidatesByKey,
        ISet<string> aliasCollisions)
    {
        if (!candidatesByKey.TryGetValue(key, out var candidates) || candidates.Count == 0)
        {
            throw new InvalidDataException($"Noto canonical PNG {size} is missing for asset key {key}");
        }

        if (candidates.Count > 1)
        {
            aliasCollisions.Add(
                $"{size}:{key}:{string.Join('|', candidates.Select(value => value.RepositoryRelativePath).Order(StringComparer.Ordinal))}");
        }

        return candidates
            .OrderByDescending(candidate => candidate.UsesCanonicalName)
            .ThenBy(candidate => candidate.RepositoryRelativePath, StringComparer.Ordinal)
            .First();
    }

    private static bool TryGetRegionFlagCode(IReadOnlyList<int> codePoints, out string code)
    {
        if (codePoints.Count == 2 && codePoints.All(value => value is >= 0x1F1E6 and <= 0x1F1FF))
        {
            code = string.Concat(codePoints.Select(value => (char)('A' + value - 0x1F1E6)));
            return true;
        }

        if (codePoints.Count >= 4 && codePoints[0] == 0x1F3F4 && codePoints[^1] == 0xE007F)
        {
            var tag = new StringBuilder();
            foreach (var value in codePoints.Skip(1).SkipLast(1))
            {
                if (value is < 0xE0061 or > 0xE007A)
                {
                    code = string.Empty;
                    return false;
                }

                tag.Append((char)('a' + value - 0xE0061));
            }

            if (tag.Length <= 2)
            {
                code = string.Empty;
                return false;
            }

            code = $"{tag.ToString(0, 2)}-{tag.ToString(2, tag.Length - 2)}".ToUpperInvariant();
            return true;
        }

        code = string.Empty;
        return false;
    }

    private static string NormalizeFilenameKey(IEnumerable<string> values)
    {
        return string.Join(
            '_',
            values
                .Select(value => Convert.ToInt32(value, 16))
                .Where(value => value != 0xFE0F)
                .Select(BaselineUtilities.FormatCodePointLower));
    }

    private static string CombineRepositoryPath(string destinationRoot, string relativePath) =>
        $"{destinationRoot.TrimEnd('/', '\\')}/{relativePath}".Replace('\\', '/');

    private static void AddCandidate(
        Dictionary<string, List<AssetCandidate>> destination,
        string key,
        AssetCandidate candidate)
    {
        if (!destination.TryGetValue(key, out var candidates))
        {
            candidates = [];
            destination.Add(key, candidates);
        }

        candidates.Add(candidate);
    }

    private static void VerifyMappedFile(string repositoryRoot, string path, string stableId, string role)
    {
        if (!File.Exists(BaselineUtilities.RepositoryPath(repositoryRoot, path)))
        {
            throw new InvalidDataException($"Mapped {role} asset is missing for {stableId}: {path}");
        }
    }
}
