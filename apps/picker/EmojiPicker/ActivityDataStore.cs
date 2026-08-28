using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EmojiPicker;

internal enum ActivityDataKind
{
    Recent,
    LearnedRanking,
}

internal sealed record ActivityRecoveryNotice(
    ActivityDataKind Kind,
    string BackupPath,
    string Message);

internal sealed record RecentActivityEntry(
    string ResolvedEntryId,
    string UnicodeSequence);

/// <summary>
/// Owns the two independent, local-only Activity Data stores. Persistence,
/// migration and corruption recovery stay behind this boundary so picker UI
/// code cannot accidentally mix Activity Data with Settings or Classic data.
/// </summary>
internal sealed class ActivityDataStore
{
    internal const int MaxRecentEntries = 50;
    internal const int CurrentRecentSchemaVersion = 1;
    internal const int CurrentRankingSchemaVersion = 1;
    internal static readonly TimeSpan RankingHalfLife = TimeSpan.FromDays(90);

    private const string RecentFileName = "recent.json";
    private const string RankingFileName = "learned-ranking.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string? directory;
    private readonly Func<DateTimeOffset> utcNow;
    private readonly Func<string, string?>? resolvedIdForSequence;
    private readonly List<RecentActivityEntry> recents = [];
    private readonly Dictionary<string, RankingState> rankings = new(StringComparer.Ordinal);
    private readonly List<ActivityRecoveryNotice> recoveryNotices = [];

    internal ActivityDataStore(
        string directory,
        Func<DateTimeOffset>? utcNow = null,
        Func<string, string?>? resolvedIdForSequence = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        this.directory = Path.GetFullPath(directory);
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        this.resolvedIdForSequence = resolvedIdForSequence;
        LoadRecent();
        LoadRanking();
    }

    private ActivityDataStore(Func<DateTimeOffset>? utcNow)
    {
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    internal static ActivityDataStore CreateTransient(Func<DateTimeOffset>? utcNow = null) => new(utcNow);

    internal IReadOnlyList<RecentActivityEntry> RecentEntries => recents;

    internal IReadOnlyList<ActivityRecoveryNotice> RecoveryNotices => recoveryNotices;

    internal bool HasRecent => recents.Count > 0;

    internal void RecordSelection(string baseEntryId, string resolvedEntryId, string unicodeSequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseEntryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedEntryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(unicodeSequence);

        recents.RemoveAll(entry =>
            string.Equals(entry.ResolvedEntryId, resolvedEntryId, StringComparison.Ordinal) ||
            string.Equals(entry.UnicodeSequence, unicodeSequence, StringComparison.Ordinal));
        recents.Insert(0, new RecentActivityEntry(resolvedEntryId, unicodeSequence));
        if (recents.Count > MaxRecentEntries)
        {
            recents.RemoveRange(MaxRecentEntries, recents.Count - MaxRecentEntries);
        }

        var now = utcNow().ToUniversalTime();
        var existing = rankings.GetValueOrDefault(baseEntryId);
        var decayedFrequency = existing == null ? 0 : Decay(existing.DecayedFrequency, existing.UpdatedAtUtc, now);
        rankings[baseEntryId] = new RankingState(decayedFrequency + 1, now);

        PersistRecent();
        PersistRanking();
    }

    internal double GetLearnedScore(string baseEntryId)
    {
        if (!rankings.TryGetValue(baseEntryId, out var state))
        {
            return 0;
        }

        return Decay(state.DecayedFrequency, state.UpdatedAtUtc, utcNow().ToUniversalTime());
    }

    internal IReadOnlyDictionary<string, double> GetLearnedScores()
    {
        var now = utcNow().ToUniversalTime();
        return rankings.ToDictionary(
            pair => pair.Key,
            pair => Decay(pair.Value.DecayedFrequency, pair.Value.UpdatedAtUtc, now),
            StringComparer.Ordinal);
    }

    internal void ClearRecent()
    {
        recents.Clear();
        PersistRecent();
    }

    internal void ResetLearnedRanking()
    {
        rankings.Clear();
        PersistRanking();
    }

    internal void ClearAllActivity()
    {
        recents.Clear();
        rankings.Clear();
        PersistRecent();
        PersistRanking();
    }

    private static double Decay(double frequency, DateTimeOffset updatedAtUtc, DateTimeOffset nowUtc)
    {
        var elapsed = nowUtc - updatedAtUtc;
        if (elapsed <= TimeSpan.Zero)
        {
            return frequency;
        }

        return frequency * Math.Pow(0.5, elapsed.TotalDays / RankingHalfLife.TotalDays);
    }

    private void LoadRecent()
    {
        var path = GetPath(RecentFileName);
        if (path == null || !File.Exists(path))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var migrated = document.RootElement.ValueKind == JsonValueKind.Array;
            var loaded = migrated
                ? MigrateLegacyRecent(document.RootElement)
                : ReadRecentSchema(document.RootElement);
            recents.AddRange(loaded
                .Where(IsValidRecent)
                .DistinctBy(entry => entry.ResolvedEntryId, StringComparer.Ordinal)
                .Take(MaxRecentEntries));
            if (migrated)
            {
                PersistRecent();
            }
        }
        catch (Exception ex)
        {
            RecoverCorruptFile(ActivityDataKind.Recent, path, ex);
        }
    }

    private void LoadRanking()
    {
        var path = GetPath(RankingFileName);
        if (path == null || !File.Exists(path))
        {
            return;
        }

        try
        {
            var schema = JsonSerializer.Deserialize<RankingSchema>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidDataException("Learned Ranking schema is empty.");
            if (schema.SchemaVersion != CurrentRankingSchemaVersion)
            {
                throw new InvalidDataException($"Unsupported Learned Ranking schema {schema.SchemaVersion}.");
            }

            foreach (var entry in schema.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.BaseEntryId) ||
                    !double.IsFinite(entry.DecayedFrequency) ||
                    entry.DecayedFrequency <= 0 ||
                    entry.UpdatedAtUtc == default)
                {
                    throw new InvalidDataException("Learned Ranking contains an invalid entry.");
                }

                rankings[entry.BaseEntryId] = new RankingState(
                    entry.DecayedFrequency,
                    entry.UpdatedAtUtc.ToUniversalTime());
            }
        }
        catch (Exception ex)
        {
            RecoverCorruptFile(ActivityDataKind.LearnedRanking, path, ex);
        }
    }

    private static IReadOnlyList<RecentActivityEntry> ReadRecentSchema(JsonElement root)
    {
        var schema = root.Deserialize<RecentSchema>(JsonOptions)
            ?? throw new InvalidDataException("Recent schema is empty.");
        if (schema.SchemaVersion != CurrentRecentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported Recent schema {schema.SchemaVersion}.");
        }

        if (schema.Entries.Any(entry => !IsValidRecent(entry)))
        {
            throw new InvalidDataException("Recent contains an invalid entry.");
        }

        return schema.Entries;
    }

    private IReadOnlyList<RecentActivityEntry> MigrateLegacyRecent(JsonElement root)
    {
        if (resolvedIdForSequence == null)
        {
            throw new InvalidDataException("Legacy Recent cannot be mapped to stable Emoji Entry identifiers.");
        }

        var migrated = new List<RecentActivityEntry>();
        foreach (var element in root.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
            {
                throw new InvalidDataException("Legacy Recent contains an invalid sequence.");
            }

            var sequence = element.GetString()!;
            var resolvedId = resolvedIdForSequence(sequence);
            if (!string.IsNullOrWhiteSpace(resolvedId))
            {
                migrated.Add(new RecentActivityEntry(resolvedId, sequence));
            }
        }

        return migrated;
    }

    private void RecoverCorruptFile(ActivityDataKind kind, string path, Exception cause)
    {
        if (kind == ActivityDataKind.Recent)
        {
            recents.Clear();
        }
        else
        {
            rankings.Clear();
        }

        var backupPath = string.Empty;
        try
        {
            backupPath = CreateCorruptBackup(path);
        }
        catch (Exception backupError)
        {
            // A permissions or filesystem error must not turn damaged optional
            // Activity Data into a process-start failure. The UI still reports
            // the reset and the log explains why a backup could not be made.
            Logger.LogAlways($"Could not back up corrupt {kind} data '{path}': {backupError.Message}");
        }

        recoveryNotices.Add(new ActivityRecoveryNotice(
            kind,
            backupPath,
            backupPath.Length == 0
                ? $"{kind} data was unreadable and was reset; the original could not be backed up."
                : kind == ActivityDataKind.Recent
                    ? "Recent data was unreadable. It was backed up and reset."
                    : "Learned Ranking data was unreadable. It was backed up and reset."));
        Logger.LogAlways($"Recovered corrupt {kind} data to '{backupPath}': {cause.Message}");

        if (kind == ActivityDataKind.Recent)
        {
            PersistRecent();
        }
        else
        {
            PersistRanking();
        }
    }

    private string CreateCorruptBackup(string path)
    {
        var suffix = utcNow().ToUniversalTime().ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
        var candidate = $"{path}.corrupt-{suffix}";
        var attempt = 2;
        while (File.Exists(candidate))
        {
            candidate = $"{path}.corrupt-{suffix}-{attempt++}";
        }

        File.Copy(path, candidate, overwrite: false);
        return candidate;
    }

    private void PersistRecent() => TryAtomicWrite(
        RecentFileName,
        new RecentSchema
        {
            SchemaVersion = CurrentRecentSchemaVersion,
            Entries = recents.ToList(),
        });

    private void PersistRanking() => TryAtomicWrite(
        RankingFileName,
        new RankingSchema
        {
            SchemaVersion = CurrentRankingSchemaVersion,
            Entries = rankings
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new RankingEntrySchema
                {
                    BaseEntryId = pair.Key,
                    DecayedFrequency = pair.Value.DecayedFrequency,
                    UpdatedAtUtc = pair.Value.UpdatedAtUtc,
                })
                .ToList(),
        });

    private void TryAtomicWrite<T>(string fileName, T value)
    {
        var path = GetPath(fileName);
        if (path == null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(directory!);
            var temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value, JsonOptions));
            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogAlways($"Could not persist Activity Data '{fileName}': {ex.Message}");
        }
    }

    private string? GetPath(string fileName) => directory == null ? null : Path.Combine(directory, fileName);

    private static bool IsValidRecent(RecentActivityEntry entry) =>
        !string.IsNullOrWhiteSpace(entry.ResolvedEntryId) &&
        !string.IsNullOrWhiteSpace(entry.UnicodeSequence);

    private sealed record RankingState(double DecayedFrequency, DateTimeOffset UpdatedAtUtc);

    private sealed class RecentSchema
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("entries")]
        public List<RecentActivityEntry> Entries { get; set; } = [];
    }

    private sealed class RankingSchema
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("entries")]
        public List<RankingEntrySchema> Entries { get; set; } = [];
    }

    private sealed class RankingEntrySchema
    {
        [JsonPropertyName("baseEntryId")]
        public string BaseEntryId { get; set; } = string.Empty;

        [JsonPropertyName("decayedFrequency")]
        public double DecayedFrequency { get; set; }

        [JsonPropertyName("updatedAtUtc")]
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }
}
