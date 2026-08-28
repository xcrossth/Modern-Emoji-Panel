using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EmojiBaseline.Generator;

internal static class BaselineUtilities
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string FormatCodePoint(int value) =>
        value.ToString(value <= 0xFFFF ? "X4" : "X", CultureInfo.InvariantCulture);

    public static string FormatCodePointLower(int value) =>
        value.ToString(value <= 0xFFFF ? "x4" : "x", CultureInfo.InvariantCulture);

    public static string SequenceKey(IEnumerable<int> codePoints, bool omitEmojiVariationSelector)
    {
        return string.Join(
            '_',
            codePoints
                .Where(value => !omitEmojiVariationSelector || value != 0xFE0F)
                .Select(FormatCodePointLower));
    }

    public static string TextKey(string text, bool omitEmojiVariationSelector)
    {
        return SequenceKey(text.EnumerateRunes().Select(rune => rune.Value), omitEmojiVariationSelector);
    }

    public static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static string RepositoryPath(string repositoryRoot, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException($"Repository path must be relative: {relativePath}");
        }

        var root = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Repository path escapes the root: {relativePath}");
        }

        return resolved;
    }

    public static string RelativeRepositoryPath(string repositoryRoot, string path) =>
        Path.GetRelativePath(repositoryRoot, path).Replace(Path.DirectorySeparatorChar, '/');

    public static void WriteDeterministicJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var json = JsonSerializer.Serialize(value, JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal);
        File.WriteAllText(path, json + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static T ReadJson<T>(string path)
    {
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Unable to deserialize JSON: {path}");
    }
}
