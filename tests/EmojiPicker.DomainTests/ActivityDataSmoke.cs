using System.Text.Json;
using EmojiPicker;

internal static class ActivityDataSmoke
{
    public static void Run()
    {
        VerifyRecentAndRanking();
        VerifyIndependentClearControls();
        VerifyLegacyRecentMigration();
        VerifyIndependentCorruptionRecovery();
        VerifySearchTierBoundary();
    }

    private static void VerifyRecentAndRanking()
    {
        var now = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
        var store = ActivityDataStore.CreateTransient(() => now);
        for (var index = 0; index < 51; index++)
        {
            store.RecordSelection($"base-{index}", $"resolved-{index}", $"sequence-{index}");
        }

        Assert(store.RecentEntries.Count == ActivityDataStore.MaxRecentEntries,
            "Recent must retain exactly the latest 50 resolved sequences.");
        Assert(store.RecentEntries[0].ResolvedEntryId == "resolved-50" &&
            store.RecentEntries[^1].ResolvedEntryId == "resolved-1",
            "Recent must use newest-first MRU ordering.");

        store.RecordSelection("base-10", "resolved-10", "sequence-10");
        Assert(store.RecentEntries[0].ResolvedEntryId == "resolved-10" &&
            store.RecentEntries.Count == ActivityDataStore.MaxRecentEntries,
            "Selecting a duplicate Recent entry must move it to the front.");

        var ranking = ActivityDataStore.CreateTransient(() => now);
        ranking.RecordSelection("person", "person-light", "person-light-sequence");
        ranking.RecordSelection("person", "person-dark", "person-dark-sequence");
        Assert(Math.Abs(ranking.GetLearnedScore("person") - 2) < 0.000001,
            "Skin-tone selections must add to one base Emoji Entry score.");
        now = now.AddDays(90);
        Assert(Math.Abs(ranking.GetLearnedScore("person") - 1) < 0.000001,
            "Learned Ranking frequency must have a 90-day half-life.");
    }

    private static void VerifyIndependentClearControls()
    {
        WithTemporaryDirectory(directory =>
        {
            var store = new ActivityDataStore(directory);
            store.RecordSelection("base-a", "resolved-a", "A");
            var reloaded = new ActivityDataStore(directory);
            Assert(reloaded.RecentEntries.Single() == new RecentActivityEntry("resolved-a", "A") &&
                reloaded.GetLearnedScore("base-a") > 0,
                "Resolved Recent and Learned Ranking must survive a new app session.");

            reloaded.ClearRecent();
            Assert(!reloaded.HasRecent && reloaded.GetLearnedScore("base-a") > 0,
                "Clear Recent must retain Learned Ranking.");

            reloaded.RecordSelection("base-b", "resolved-b", "B");
            reloaded.ResetLearnedRanking();
            Assert(reloaded.HasRecent && reloaded.GetLearnedScore("base-b") == 0,
                "Reset learned ranking must retain Recent.");

            reloaded.RecordSelection("base-c", "resolved-c", "C");
            reloaded.ClearAllActivity();
            Assert(!reloaded.HasRecent && reloaded.GetLearnedScore("base-c") == 0,
                "Clear all activity must clear both independent data sets.");
            Assert(!Directory.EnumerateFiles(directory, "*.tmp").Any(),
                "Atomic Activity Data writes must not leave temporary files.");

            using var recent = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "recent.json")));
            using var ranking = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "learned-ranking.json")));
            Assert(recent.RootElement.GetProperty("schemaVersion").GetInt32() ==
                ActivityDataStore.CurrentRecentSchemaVersion,
                "Recent persistence must declare its schema version.");
            Assert(ranking.RootElement.GetProperty("schemaVersion").GetInt32() ==
                ActivityDataStore.CurrentRankingSchemaVersion,
                "Learned Ranking persistence must declare its schema version.");
        });
    }

    private static void VerifyLegacyRecentMigration()
    {
        WithTemporaryDirectory(directory =>
        {
            File.WriteAllText(Path.Combine(directory, "recent.json"), "[\"actual-one\",\"actual-two\"]");
            var store = new ActivityDataStore(
                directory,
                resolvedIdForSequence: sequence => sequence == "actual-one" ? "entry-one" : "entry-two");
            Assert(store.RecentEntries.Select(entry => entry.ResolvedEntryId)
                    .SequenceEqual(["entry-one", "entry-two"]),
                "Modern's legacy Recent list must migrate to stable resolved identifiers.");

            using var migrated = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "recent.json")));
            Assert(migrated.RootElement.ValueKind == JsonValueKind.Object &&
                migrated.RootElement.GetProperty("schemaVersion").GetInt32() == 1,
                "Legacy Recent migration must atomically replace the old shape with the versioned schema.");
        });
    }

    private static void VerifyIndependentCorruptionRecovery()
    {
        WithTemporaryDirectory(directory =>
        {
            var now = new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
            var original = new ActivityDataStore(directory, () => now);
            original.RecordSelection("base-a", "resolved-a", "A");
            File.WriteAllText(Path.Combine(directory, "recent.json"), "{not-json");

            var recoveredRecent = new ActivityDataStore(directory, () => now);
            Assert(!recoveredRecent.HasRecent && recoveredRecent.GetLearnedScore("base-a") > 0,
                "Corrupt Recent must reset only Recent and retain Learned Ranking.");
            Assert(recoveredRecent.RecoveryNotices.Count == 1 &&
                recoveredRecent.RecoveryNotices[0].Kind == ActivityDataKind.Recent &&
                File.Exists(recoveredRecent.RecoveryNotices[0].BackupPath),
                "Corrupt Recent must create a timestamped backup and a user notice.");

            recoveredRecent.RecordSelection("base-b", "resolved-b", "B");
            File.WriteAllText(Path.Combine(directory, "learned-ranking.json"), "[]");
            var recoveredRanking = new ActivityDataStore(directory, () => now);
            Assert(recoveredRanking.HasRecent && recoveredRanking.GetLearnedScore("base-b") == 0,
                "Corrupt Learned Ranking must reset only ranking and retain Recent.");
            Assert(recoveredRanking.RecoveryNotices.Count == 1 &&
                recoveredRanking.RecoveryNotices[0].Kind == ActivityDataKind.LearnedRanking &&
                File.Exists(recoveredRanking.RecoveryNotices[0].BackupPath),
                "Corrupt Learned Ranking must create a timestamped backup and a user notice.");
        });
    }

    private static void VerifySearchTierBoundary()
    {
        var store = ActivityDataStore.CreateTransient();
        var exactEarly = Fixture("exact-early", "heart", 1);
        var exactLate = Fixture("exact-late", "heart", 50);
        var prefix = Fixture("prefix", "heart symbol", 0);
        for (var index = 0; index < 5; index++)
        {
            store.RecordSelection(exactLate.Id, exactLate.ResolvedEntryId, exactLate.Character);
        }

        for (var index = 0; index < 100; index++)
        {
            store.RecordSelection(prefix.Id, prefix.ResolvedEntryId, prefix.Character);
        }

        var matches = new EmojiSearchIndex([prefix, exactEarly, exactLate], store.GetLearnedScores).Search("heart");
        Assert(matches[0].Emoji.Id == exactLate.Id && matches[1].Emoji.Id == exactEarly.Id,
            "Learned Ranking must reorder Emoji Entries inside one match tier.");
        Assert(matches[2].Emoji.Id == prefix.Id && matches[2].Tier == EmojiMatchTier.ShortNameTermPrefix,
            "A heavily learned lower-quality match must never overtake an exact short name.");
    }

    private static Emoji Fixture(string id, string englishName, int order) => new(
        id,
        id,
        englishName,
        englishName,
        englishName,
        "Symbols",
        id,
        [],
        [],
        "17.0",
        "asset.png",
        "preview.png",
        order,
        popularity: 0);

    private static void WithTemporaryDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"modern-emoji-picker-activity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            action(directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
