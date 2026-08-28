using EmojiPicker;

var repositoryRoot = args.Length == 1
    ? Path.GetFullPath(args[0])
    : throw new ArgumentException("Pass the repository root as the only argument.");

var catalog = EmojiCatalog.Load(repositoryRoot);
Assert(catalog.BaselineAvailable, "Emoji Baseline must be available to the domain verification.");
Assert(catalog.Entries.Count == 3_944, "Expected every fully-qualified Emoji 17 sequence.");

var variants = new EmojiVariantCatalog(catalog.Entries);
var baselineIds = catalog.Entries.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
var baseIds = variants.BaseEntries.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);

var neutralHand = Find("1F64B");
var darkHand = Find("1F64B 1F3FF");
var neutralSelection = variants.Resolve(neutralHand, SkinTonePreference.Neutral);
Assert(neutralSelection.ResolvedEntry.Id == neutralHand.Id, "Neutral must keep the yellow base sequence.");
var darkSelection = variants.Resolve(neutralHand, SkinTonePreference.Dark);
Assert(darkSelection.ResolvedEntry.Id == darkHand.Id, "Global dark tone must resolve to the pinned baseline entry.");
Assert(baselineIds.Contains(darkSelection.ResolvedEntry.Id), "Resolved skin tone must be fully-qualified baseline data.");

var grin = Find("1F600");
Assert(!variants.SupportsSkinTone(grin), "A grinning face must not expose a skin-tone choice.");
Assert(variants.Resolve(grin, SkinTonePreference.Dark).ResolvedEntry.Id == grin.Id,
    "An entry without modifier support must not change under a global tone.");

var holdingHands = Find("1F9D1 200D 1F91D 200D 1F9D1");
var mediumHoldingHands = Find("1F9D1 1F3FD 200D 1F91D 200D 1F9D1 1F3FD");
Assert(variants.Resolve(holdingHands, SkinTonePreference.Medium).ResolvedEntry.Id == mediumHoldingHands.Id,
    "A multi-person uniform tone must apply the global setting to every modifier position.");

var mixedHoldingHands = Find("1F9D1 1F3FB 200D 1F91D 200D 1F9D1 1F3FF");
var mixedSelection = variants.Resolve(
    holdingHands,
    SkinTonePreference.Medium,
    mixedHoldingHands.Id);
Assert(mixedSelection.IsVariantOverride && mixedSelection.ResolvedEntry.Id == mixedHoldingHands.Id,
    "Variant Override must select the exact mixed-tone baseline sequence.");
var afterOverride = variants.Resolve(holdingHands, SkinTonePreference.Medium);
Assert(!afterOverride.IsVariantOverride && afterOverride.ResolvedEntry.Id == mediumHoldingHands.Id,
    "Variant Override must be one-shot and leave the global setting unchanged.");
AssertThrows<ArgumentException>(
    () => variants.Resolve(holdingHands, SkinTonePreference.Medium, grin.Id),
    "Variant Override must reject an entry outside the selected family.");

var handshake = Find("1F91D");
var mixedHandshake = Find("1FAF1 1F3FB 200D 1FAF2 1F3FF");
Assert(variants.GetVariantOverrides(handshake).Any(entry => entry.Id == mixedHandshake.Id),
    "Legacy neutral handshake must expose every modern mixed-tone handshake override.");

foreach (var entry in catalog.Entries)
{
    var modifiers = Modifiers(entry).ToList();
    if (modifiers.Count == 0)
    {
        Assert(baseIds.Contains(entry.Id), $"Non-tone entry {entry.CanonicalSequence} disappeared from browse access.");
        continue;
    }

    var restored = variants.RestoreResolved(entry);
    if (modifiers.Distinct(StringComparer.Ordinal).Count() == 1)
    {
        var preference = PreferenceFor(modifiers[0]);
        var resolved = variants.Resolve(restored.BaseEntry, preference);
        Assert(resolved.ResolvedEntry.Id == entry.Id,
            $"Uniform variant {entry.CanonicalSequence} is not reachable through global skin tone.");
    }
    else
    {
        Assert(variants.GetVariantOverrides(restored.BaseEntry).Any(candidate => candidate.Id == entry.Id),
            $"Mixed-tone variant {entry.CanonicalSequence} is not reachable through Variant Override.");
    }
}

Assert(baseIds.Contains(Find("1F1F9 1F1ED").Id), "Thailand flag must remain browsable.");
Assert(baseIds.Contains(Find("0031 FE0F 20E3").Id), "Keycap sequence must remain browsable.");
Assert(baseIds.Contains(Find("1F468 200D 1F469 200D 1F467").Id), "Complex ZWJ family must remain browsable.");

var defaultSettings = new Settings();
Assert(defaultSettings.PreferredSkinTone == SkinTonePreference.Neutral,
    "A fresh profile must default to neutral yellow.");
var settingsRoot = Path.Combine(Path.GetTempPath(), $"modern-emoji-picker-variant-tests-{Guid.NewGuid():N}");
var settingsPath = Path.Combine(settingsRoot, "settings.json");
try
{
    defaultSettings.GlobalSkinTone = SkinTonePreference.MediumDark.ToSettingValue();
    defaultSettings.SaveTo(settingsPath);
    var reloaded = Settings.LoadFrom(settingsPath);
    Assert(reloaded.PreferredSkinTone == SkinTonePreference.MediumDark,
        "Global skin tone must persist across Picker Sessions.");
    reloaded.GlobalSkinTone = SkinTonePreference.Light.ToSettingValue();
    reloaded.SaveTo(settingsPath);
    Assert(Settings.LoadFrom(settingsPath).PreferredSkinTone == SkinTonePreference.Light,
        "Changing the global skin tone must atomically replace the persisted setting.");
}
finally
{
    if (Directory.Exists(settingsRoot))
    {
        Directory.Delete(settingsRoot, recursive: true);
    }
}

Console.WriteLine(
    $"Emoji variant verification passed: {catalog.Entries.Count} baseline sequences, " +
    $"{variants.BaseEntries.Count} browse entries, global tone and one-shot mixed overrides");
return;

Emoji Find(string canonicalSequence) => catalog.Entries.Single(entry =>
    string.Equals(entry.CanonicalSequence, canonicalSequence, StringComparison.Ordinal));

static IEnumerable<string> Modifiers(Emoji entry) => entry.CanonicalSequence
    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
    .Where(codePoint => codePoint is "1F3FB" or "1F3FC" or "1F3FD" or "1F3FE" or "1F3FF");

static SkinTonePreference PreferenceFor(string codePoint) => codePoint switch
{
    "1F3FB" => SkinTonePreference.Light,
    "1F3FC" => SkinTonePreference.MediumLight,
    "1F3FD" => SkinTonePreference.Medium,
    "1F3FE" => SkinTonePreference.MediumDark,
    "1F3FF" => SkinTonePreference.Dark,
    _ => throw new ArgumentOutOfRangeException(nameof(codePoint)),
};

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}
