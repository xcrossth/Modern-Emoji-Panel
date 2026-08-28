using System.Globalization;
using System.IO;
using System.Text.Json;

namespace EmojiPicker;

internal sealed record EmojiCatalogLoadResult(
    IReadOnlyList<Emoji> Entries,
    bool BaselineAvailable,
    bool AssetSetAvailable,
    string? ErrorMessage);

internal static class EmojiCatalog
{
    internal const string BaselineRelativePath = "data/emoji-baseline/17.0/emoji.json";

    public static string AssetRoot { get; } = Path.Combine(AppContext.BaseDirectory, "EmojiBaseline");

    public static EmojiCatalogLoadResult Load() => Load(AssetRoot);

    internal static EmojiCatalogLoadResult Load(string assetRoot)
    {
        var baselinePath = ResolveBundledPath(assetRoot, BaselineRelativePath);
        if (!File.Exists(baselinePath))
        {
            return new EmojiCatalogLoadResult([], false, false, "The bundled Emoji Baseline is missing.");
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(baselinePath));
            var entries = new List<Emoji>(capacity: 4_000);
            foreach (var element in document.RootElement.GetProperty("entries").EnumerateArray())
            {
                var english = element.GetProperty("english");
                var thai = element.GetProperty("thai");
                var asset = element.GetProperty("asset");
                var englishName = english.GetProperty("shortName").GetString() ?? string.Empty;
                var thaiName = thai.GetProperty("shortName").GetString() ?? englishName;
                var englishKeywords = ReadKeywords(english);
                var thaiKeywords = ReadKeywords(thai);
                var localizedName = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "th"
                    ? thaiName
                    : englishName;

                entries.Add(new Emoji(
                    id: element.GetProperty("id").GetString() ?? throw new InvalidDataException("Emoji id is missing."),
                    character: element.GetProperty("text").GetString() ?? throw new InvalidDataException("Emoji text is missing."),
                    name: localizedName,
                    englishName: englishName,
                    thaiName: thaiName,
                    category: element.GetProperty("group").GetString() ?? string.Empty,
                    canonicalSequence: element.GetProperty("canonicalSequence").GetString() ?? string.Empty,
                    englishKeywords: englishKeywords,
                    thaiKeywords: thaiKeywords,
                    emojiVersion: element.GetProperty("emojiVersion").GetString() ?? string.Empty,
                    assetPath: asset.GetProperty("png128").GetString() ?? string.Empty,
                    previewAssetPath: asset.GetProperty("png512").GetString() ?? string.Empty,
                    order: element.GetProperty("order").GetInt32(),
                    popularity: 99));
            }

            var canonicalAssetDirectory = ResolveBundledPath(assetRoot, "vendor/noto-emoji/v2.051/png/128");
            var previewAssetDirectory = ResolveBundledPath(assetRoot, "vendor/noto-emoji/v2.051/png/512");
            var regionFlagDirectory = ResolveBundledPath(assetRoot, "vendor/noto-emoji/v2.051/third_party/region-flags/png");
            var assetSetAvailable = Directory.Exists(canonicalAssetDirectory) &&
                Directory.EnumerateFiles(canonicalAssetDirectory, "*.png").Any() &&
                Directory.Exists(previewAssetDirectory) &&
                Directory.EnumerateFiles(previewAssetDirectory, "*.png").Any() &&
                Directory.Exists(regionFlagDirectory) &&
                Directory.EnumerateFiles(regionFlagDirectory, "*.png").Any();

            return new EmojiCatalogLoadResult(entries, true, assetSetAvailable,
                assetSetAvailable ? null : "The bundled Noto artwork is missing.");
        }
        catch (Exception ex)
        {
            return new EmojiCatalogLoadResult([], false, false, $"The bundled Emoji Baseline could not be read: {ex.Message}");
        }
    }

    public static string ResolveBundledPath(string relativePath)
        => ResolveBundledPath(AssetRoot, relativePath);

    private static string ResolveBundledPath(string assetRoot, string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(assetRoot, normalized));
    }

    private static IReadOnlyList<string> ReadKeywords(JsonElement language)
    {
        return language.GetProperty("keywords")
            .EnumerateArray()
            .Select(keyword => keyword.GetString())
            .OfType<string>()
            .Where(keyword => keyword.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
