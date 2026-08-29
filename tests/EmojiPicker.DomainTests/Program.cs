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
var family = Find("1F468 200D 1F469 200D 1F466");
Assert(baseIds.Contains(family.Id), "Complex ZWJ family must remain browsable.");
var lightFamily = variants.Resolve(family, SkinTonePreference.Light);
Assert(variants.SupportsSkinTone(family), "An explicit family must follow the global skin tone.");
Assert(lightFamily.ResolvedEntry.Character == "👨🏻‍👩🏻‍👦🏻",
    "Global light tone must apply to every explicit family member.");
Assert(lightFamily.ResolvedEntry.CanonicalSequence ==
    "1F468 1F3FB 200D 1F469 1F3FB 200D 1F466 1F3FB",
    "Family tone resolution must preserve the ZWJ structure and add every modifier.");
Assert(lightFamily.ResolvedEntry.AssetPath.StartsWith("generated/family-tone/128/", StringComparison.Ordinal),
    "A toned family must use generated Noto composite artwork in the grid.");
Assert(lightFamily.ResolvedEntry.PreviewAssetPath.StartsWith("generated/family-tone/512/", StringComparison.Ordinal),
    "A toned family must use generated Noto composite artwork in preview.");
var familyBases = variants.BaseEntries
    .Where(entry => entry.EnglishName.StartsWith("family", StringComparison.OrdinalIgnoreCase))
    .ToArray();
Assert(familyBases.Length == 30, "Expected all 30 explicit and generic family entries in browse data.");
var neutralFamilyPresentations = familyBases
    .Select(entry => variants.Resolve(entry, SkinTonePreference.Neutral).ToPresentation())
    .ToArray();
Assert(neutralFamilyPresentations.All(entry =>
        entry.AssetPath.StartsWith("generated/family-tone/128/neutral/", StringComparison.Ordinal) &&
        entry.Character == familyBases.Single(baseEntry => baseEntry.Id == entry.Id).Character),
    "Neutral families must use yellow Noto composites while preserving their baseline sequence.");
var tonePreferences = Enum.GetValues<SkinTonePreference>()
    .Where(preference => preference != SkinTonePreference.Neutral)
    .ToArray();
var derivedFamilyEntries = new List<Emoji>(familyBases.Length * tonePreferences.Length);
foreach (var familyBase in familyBases)
{
    foreach (var preference in tonePreferences)
    {
        var resolved = variants.Resolve(familyBase, preference);
        Assert(resolved.ResolvedEntry.Id != familyBase.Id,
            $"Family {familyBase.CanonicalSequence} did not resolve for {preference}.");
        Assert(resolved.ResolvedEntry.AssetPath.StartsWith(FamilyToneVariants.AssetPrefix, StringComparison.Ordinal),
            $"Family {familyBase.CanonicalSequence} did not use composite artwork for {preference}.");
        Assert(variants.TryRestore(resolved.ResolvedEntry.Id, resolved.ResolvedEntry.Character)?.ResolvedEntry.Id ==
            resolved.ResolvedEntry.Id,
            $"Family {familyBase.CanonicalSequence} {preference} could not be restored for Recent.");
        var modifiers = Modifiers(resolved.ResolvedEntry).ToArray();
        Assert(modifiers.Length is >= 2 and <= 4 && modifiers.Distinct(StringComparer.Ordinal).Count() == 1,
            $"Family {familyBase.CanonicalSequence} did not apply one uniform tone to every member.");
        derivedFamilyEntries.Add(resolved.ResolvedEntry);
    }
}
Assert(variants.ResolvedEntryIds.Count == catalog.Entries.Count + (familyBases.Length * tonePreferences.Length),
    "Variant catalog must expose every derived family ID for Activity Data pruning.");
var allFamilyGridArtwork = Task.WhenAll(derivedFamilyEntries.Select(entry =>
        NotoEmojiAssetProvider.Shared.LoadAsync(entry.AssetPath, 32)))
    .GetAwaiter()
    .GetResult();
Assert(allFamilyGridArtwork.All(image => image is { IsFrozen: true }),
    "Every derived family/tone combination must render from bundled Noto member artwork.");
var allNeutralFamilyArtwork = Task.WhenAll(neutralFamilyPresentations.Select(entry =>
        NotoEmojiAssetProvider.Shared.LoadAsync(entry.AssetPath, 32)))
    .GetAwaiter()
    .GetResult();
Assert(allNeutralFamilyArtwork.All(image => image is { IsFrozen: true }),
    "Every neutral family must render a yellow composite from bundled Noto member artwork.");

var familyGridArtwork = NotoEmojiAssetProvider.Shared
    .LoadAsync(lightFamily.ResolvedEntry.AssetPath, 128)
    .GetAwaiter()
    .GetResult() as System.Windows.Media.Imaging.BitmapSource;
var familyPreviewArtwork = NotoEmojiAssetProvider.Shared
    .LoadAsync(lightFamily.ResolvedEntry.PreviewAssetPath, 160)
    .GetAwaiter()
    .GetResult() as System.Windows.Media.Imaging.BitmapSource;
Assert(familyGridArtwork is { IsFrozen: true, PixelWidth: 128 } && HasColoredPixel(familyGridArtwork),
    "Generated family grid artwork must be frozen, correctly sized and colored.");
Assert(familyPreviewArtwork is { IsFrozen: true, PixelWidth: 160 } && HasColoredPixel(familyPreviewArtwork),
    "Generated family preview artwork must be frozen, correctly sized and colored.");

ActivityDataSmoke.Run();
SettingsPrivacySmoke.Run();

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
    $"{variants.BaseEntries.Count} browse entries, {derivedFamilyEntries.Count} derived family tones, " +
    $"{neutralFamilyPresentations.Length} neutral family composites, " +
    "global tone, mixed overrides and local Activity Data");
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

static bool HasColoredPixel(System.Windows.Media.Imaging.BitmapSource source)
{
    var converted = new System.Windows.Media.Imaging.FormatConvertedBitmap(
        source,
        System.Windows.Media.PixelFormats.Bgra32,
        destinationPalette: null,
        alphaThreshold: 0);
    var stride = converted.PixelWidth * 4;
    var pixels = new byte[stride * converted.PixelHeight];
    converted.CopyPixels(pixels, stride, 0);
    for (var index = 0; index < pixels.Length; index += 4)
    {
        var blue = pixels[index];
        var green = pixels[index + 1];
        var red = pixels[index + 2];
        var alpha = pixels[index + 3];
        if (alpha > 0 && (red != green || green != blue))
        {
            return true;
        }
    }

    return false;
}

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
