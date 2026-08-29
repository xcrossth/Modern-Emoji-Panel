using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EmojiPicker;

/// <summary>
/// Builds uniform-tone family selections and renders their artwork from the
/// pinned Noto member portraits. Unicode does not publish these combinations as
/// RGI baseline entries, so they remain a derived presentation layer rather
/// than being added to Emoji Baseline.
/// </summary>
internal static class FamilyToneVariants
{
    internal const string AssetPrefix = "generated/family-tone/";

    private static readonly HashSet<string> MemberCodePoints =
    [
        "1F468", // man
        "1F469", // woman
        "1F466", // boy
        "1F467", // girl
        "1F9D1", // adult
        "1F9D2", // child
    ];

    internal static IReadOnlyList<Emoji> CreateUniformVariants(Emoji baseEntry)
    {
        if (!baseEntry.EnglishName.StartsWith("family", StringComparison.OrdinalIgnoreCase) ||
            !TryGetMembers(baseEntry.CanonicalSequence, out var members))
        {
            return [];
        }

        return Enum.GetValues<SkinTonePreference>()
            .Where(preference => preference != SkinTonePreference.Neutral)
            .Select(preference => CreateVariant(baseEntry, members, preference))
            .ToArray();
    }

    internal static bool TryGetNeutralAssetPaths(
        Emoji baseEntry,
        out string gridAssetPath,
        out string previewAssetPath)
    {
        if (!baseEntry.EnglishName.StartsWith("family", StringComparison.OrdinalIgnoreCase) ||
            !TryGetMembers(baseEntry.CanonicalSequence, out var members))
        {
            gridAssetPath = string.Empty;
            previewAssetPath = string.Empty;
            return false;
        }

        var memberKey = string.Join('_', members).ToLowerInvariant();
        gridAssetPath = $"{AssetPrefix}128/neutral/{memberKey}.png";
        previewAssetPath = $"{AssetPrefix}512/neutral/{memberKey}.png";
        return true;
    }

    internal static ImageSource? TryRender(string relativePath, int decodePixelWidth)
    {
        if (!TryParseAssetPath(relativePath, out var sourceSize, out var tone, out var members))
        {
            return null;
        }

        var outputSize = Math.Clamp(decodePixelWidth, 16, 512);
        using var canvas = new System.Drawing.Bitmap(
            outputSize,
            outputSize,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = System.Drawing.Graphics.FromImage(canvas);
        graphics.Clear(System.Drawing.Color.Transparent);
        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;

        var layout = Layout(members.Count, outputSize);
        for (var index = 0; index < members.Count; index++)
        {
            var toneSuffix = string.Equals(tone, "neutral", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : $"_{tone.ToLowerInvariant()}";
            var sourcePath = EmojiCatalog.ResolveBundledPath(
                $"vendor/noto-emoji/v2.051/png/{sourceSize}/emoji_u{members[index].ToLowerInvariant()}{toneSuffix}.png");
            if (!File.Exists(sourcePath))
            {
                return null;
            }

            using var member = System.Drawing.Image.FromFile(sourcePath);
            graphics.DrawImage(member, layout[index]);
        }

        using var stream = new MemoryStream();
        canvas.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static Emoji CreateVariant(
        Emoji baseEntry,
        IReadOnlyList<string> members,
        SkinTonePreference preference)
    {
        var tone = ToneCodePoint(preference);
        var canonicalSequence = string.Join(" 200D ", members.Select(member => $"{member} {tone}"));
        var character = string.Concat(canonicalSequence
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(codePoint => char.ConvertFromUtf32(
                int.Parse(codePoint, NumberStyles.HexNumber, CultureInfo.InvariantCulture))));
        var memberKey = string.Join('_', members).ToLowerInvariant();
        var toneKey = tone.ToLowerInvariant();
        var settingKey = preference.ToSettingValue();
        return new Emoji(
            id: $"{baseEntry.Id}--uniform-{settingKey}",
            character: character,
            name: baseEntry.Name,
            englishName: baseEntry.EnglishName,
            thaiName: baseEntry.ThaiName,
            category: baseEntry.Category,
            canonicalSequence: canonicalSequence,
            englishKeywords: baseEntry.EnglishKeywords,
            thaiKeywords: baseEntry.ThaiKeywords,
            emojiVersion: baseEntry.EmojiVersion,
            assetPath: $"{AssetPrefix}128/{toneKey}/{memberKey}.png",
            previewAssetPath: $"{AssetPrefix}512/{toneKey}/{memberKey}.png",
            order: baseEntry.Order,
            popularity: baseEntry.Popularity,
            baseCanonicalSequence: baseEntry.CanonicalSequence,
            resolvedEntryId: $"{baseEntry.Id}--uniform-{settingKey}");
    }

    private static bool TryGetMembers(string canonicalSequence, out IReadOnlyList<string> members)
    {
        // U+1F46A is the legacy generic family. Treat it as the gender-neutral
        // adult/adult/child family when a concrete skin tone is requested.
        if (string.Equals(canonicalSequence, "1F46A", StringComparison.Ordinal))
        {
            members = ["1F9D1", "1F9D1", "1F9D2"];
            return true;
        }

        var tokens = canonicalSequence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length is < 3 or > 7 || tokens.Length % 2 == 0)
        {
            members = [];
            return false;
        }

        var parsedMembers = new List<string>((tokens.Length + 1) / 2);
        for (var index = 0; index < tokens.Length; index++)
        {
            if (index % 2 == 0)
            {
                if (!MemberCodePoints.Contains(tokens[index]))
                {
                    members = [];
                    return false;
                }

                parsedMembers.Add(tokens[index]);
            }
            else if (!string.Equals(tokens[index], "200D", StringComparison.Ordinal))
            {
                members = [];
                return false;
            }
        }

        members = parsedMembers;
        return parsedMembers.Count is >= 2 and <= 4;
    }

    private static bool TryParseAssetPath(
        string relativePath,
        out int sourceSize,
        out string tone,
        out IReadOnlyList<string> members)
    {
        sourceSize = 0;
        tone = string.Empty;
        members = [];
        if (!relativePath.StartsWith(AssetPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = relativePath[AssetPrefix.Length..].Split('/');
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out sourceSize) ||
            sourceSize is not (128 or 512) ||
            !IsToneKey(parts[1]))
        {
            return false;
        }

        tone = parts[1];
        var memberFile = Path.GetFileNameWithoutExtension(parts[2]);
        var parsedMembers = memberFile.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parsedMembers.Length is < 2 or > 4 ||
            parsedMembers.Any(member => !MemberCodePoints.Contains(member.ToUpperInvariant())))
        {
            return false;
        }

        members = parsedMembers;
        return true;
    }

    private static System.Drawing.RectangleF[] Layout(int memberCount, int size)
    {
        float Scale(float value) => value * size;
        return memberCount switch
        {
            2 =>
            [
                new(Scale(-0.01f), Scale(0.18f), Scale(0.62f), Scale(0.62f)),
                new(Scale(0.39f), Scale(0.18f), Scale(0.62f), Scale(0.62f)),
            ],
            3 =>
            [
                new(Scale(0.00f), Scale(0.00f), Scale(0.58f), Scale(0.58f)),
                new(Scale(0.42f), Scale(0.00f), Scale(0.58f), Scale(0.58f)),
                new(Scale(0.21f), Scale(0.42f), Scale(0.58f), Scale(0.58f)),
            ],
            4 =>
            [
                new(Scale(0.00f), Scale(0.00f), Scale(0.52f), Scale(0.52f)),
                new(Scale(0.48f), Scale(0.00f), Scale(0.52f), Scale(0.52f)),
                new(Scale(0.00f), Scale(0.48f), Scale(0.52f), Scale(0.52f)),
                new(Scale(0.48f), Scale(0.48f), Scale(0.52f), Scale(0.52f)),
            ],
            _ => [],
        };
    }

    private static string ToneCodePoint(SkinTonePreference preference) => preference switch
    {
        SkinTonePreference.Light => "1F3FB",
        SkinTonePreference.MediumLight => "1F3FC",
        SkinTonePreference.Medium => "1F3FD",
        SkinTonePreference.MediumDark => "1F3FE",
        SkinTonePreference.Dark => "1F3FF",
        _ => throw new ArgumentOutOfRangeException(nameof(preference)),
    };

    private static bool IsToneKey(string value) =>
        string.Equals(value, "neutral", StringComparison.OrdinalIgnoreCase) ||
        value.ToUpperInvariant() is "1F3FB" or "1F3FC" or "1F3FD" or "1F3FE" or "1F3FF";
}
