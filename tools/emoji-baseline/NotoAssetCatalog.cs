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

    private static readonly Regex RegionFlagAliasPattern = new(
        "^([A-Z]{2}(?:-[A-Z0-9]+)?)\\.png$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

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

        var resolvedRegionFlags = regionFlags.ToDictionary(
            pair => pair.Key,
            pair => ResolveRegionFlagPath(repositoryRoot, pair.Key, regionFlags),
            StringComparer.OrdinalIgnoreCase);

        var mappings = new Dictionary<string, AssetMapping>(StringComparer.Ordinal);
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in emoji)
        {
            AssetMapping mapping;
            if (TryGetRegionFlagCode(entry.CodePoints, out var regionCode))
            {
                mapping = BuildRegionFlagMapping(entry, regionCode, resolvedRegionFlags, selectedAssetPaths);
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

            VerifyMappedPngFile(repositoryRoot, mapping.Png128, entry.StableId, "128/grid");
            VerifyMappedPngFile(repositoryRoot, mapping.Png512, entry.StableId, "512/preview");
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

    private static string ResolveRegionFlagPath(
        string repositoryRoot,
        string regionCode,
        IReadOnlyDictionary<string, string> regionFlags)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentCode = regionCode;
        while (true)
        {
            if (!visited.Add(currentCode))
            {
                throw new InvalidDataException(
                    $"Noto region-flag alias cycle for {regionCode}: {string.Join(" -> ", visited)}");
            }

            if (!regionFlags.TryGetValue(currentCode, out var path))
            {
                throw new InvalidDataException(
                    $"Noto region-flag alias target is missing for {regionCode}: {currentCode}.png");
            }

            var absolutePath = BaselineUtilities.RepositoryPath(repositoryRoot, path);
            if (HasPngSignature(absolutePath))
            {
                return path;
            }

            var aliasText = File.ReadAllText(absolutePath).Trim();
            var match = RegionFlagAliasPattern.Match(aliasText);
            if (!match.Success)
            {
                throw new InvalidDataException(
                    $"Noto region flag is neither PNG artwork nor a safe alias for {regionCode}: {path}");
            }

            currentCode = match.Groups[1].Value;
        }
    }

    private static void VerifyMappedPngFile(string repositoryRoot, string path, string stableId, string role)
    {
        var absolutePath = BaselineUtilities.RepositoryPath(repositoryRoot, path);
        if (!File.Exists(absolutePath))
        {
            throw new InvalidDataException($"Mapped {role} asset is missing for {stableId}: {path}");
        }

        if (!HasPngSignature(absolutePath))
        {
            throw new InvalidDataException($"Mapped {role} asset is not PNG artwork for {stableId}: {path}");
        }
    }

    private static bool HasPngSignature(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        Span<byte> header = stackalloc byte[PngSignature.Length];
        using var stream = File.OpenRead(path);
        return stream.Read(header) == header.Length && header.SequenceEqual(PngSignature);
    }
}
