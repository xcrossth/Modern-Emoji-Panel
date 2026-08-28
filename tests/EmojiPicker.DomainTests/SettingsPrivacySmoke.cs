using System.Globalization;
using EmojiPicker;

internal static class SettingsPrivacySmoke
{
    internal static void Run()
    {
        VerifySafeDefaults();
        VerifyLanguageFallback();
        VerifyThemeAndInsertionChoices();
        VerifyPersistenceAndAdvancedReset();
    }

    private static void VerifySafeDefaults()
    {
        var settings = new Settings();
        Assert(settings.HotkeyEnabled && settings.ParsedHotkey == HotkeyBinding.Default,
            "A fresh profile must enable Win + . through a validated hotkey binding.");
        Assert(settings.LanguagePreference == UiLanguagePreference.System,
            "A fresh profile must follow the supported Windows display language.");
        Assert(settings.ThemePreference == AppThemePreference.System,
            "A fresh profile must follow the Windows app theme.");
        Assert(settings.InsertMode == EmojiInsertMode.Hybrid,
            "Hybrid insertion must remain the safe default.");
        Assert(settings.PasteRestoreDelayMs == Settings.DefaultPasteRestoreDelayMs,
            "Temporary Paste must use the documented default restore delay.");
        Assert(!settings.DiagnosticLoggingEnabled && !settings.WelcomeShown,
            "Diagnostic logging must be opt-in and Welcome must be pending for a fresh profile.");
        Assert(ProductIdentity.DataDirectory.EndsWith(ProductIdentity.DataDirectoryName, StringComparison.Ordinal),
            "Settings and Activity Data must share Modern's product-scoped local directory.");
    }

    private static void VerifyLanguageFallback()
    {
        Assert(Localizer.ResolveCulture(UiLanguagePreference.System, CultureInfo.GetCultureInfo("th-TH"))
                .TwoLetterISOLanguageName == "th",
            "Thai Windows display language must select Thai UI.");
        Assert(Localizer.ResolveCulture(UiLanguagePreference.System, CultureInfo.GetCultureInfo("ja-JP"))
                .TwoLetterISOLanguageName == "en",
            "Every unsupported Windows display language must fall back to English.");
        Assert(Localizer.ResolveCulture(UiLanguagePreference.English, CultureInfo.GetCultureInfo("th-TH"))
                .TwoLetterISOLanguageName == "en" &&
            Localizer.ResolveCulture(UiLanguagePreference.Thai, CultureInfo.GetCultureInfo("en-US"))
                .TwoLetterISOLanguageName == "th",
            "The explicit English and Thai choices must override Windows.");
    }

    private static void VerifyThemeAndInsertionChoices()
    {
        Assert(!ThemeManager.ResolveDark(AppThemePreference.Light, systemDark: true) &&
            ThemeManager.ResolveDark(AppThemePreference.Dark, systemDark: false) &&
            ThemeManager.ResolveDark(AppThemePreference.System, systemDark: true),
            "System, Light and Dark theme choices must resolve deterministically.");
        Assert(
            ThemeManager.ResolveThemeUri(AppThemePreference.Light, systemDark: false, highContrast: true)
                .OriginalString.EndsWith("HighContrastTheme.xaml", StringComparison.Ordinal),
            "High Contrast must override both explicit Light and Dark themes.");
        Assert(
            ThemeManager.ResolveThemeUri(AppThemePreference.System, systemDark: true, highContrast: false)
                .OriginalString.EndsWith("DarkTheme.xaml", StringComparison.Ordinal),
            "System theme must still resolve to Dark when High Contrast is off.");
        Assert(
            ThemeManager.ShouldRefreshFor(Microsoft.Win32.UserPreferenceCategory.Accessibility) &&
            ThemeManager.ShouldRefreshFor(Microsoft.Win32.UserPreferenceCategory.Color) &&
            ThemeManager.ShouldRefreshFor(Microsoft.Win32.UserPreferenceCategory.General) &&
            ThemeManager.ShouldRefreshFor(Microsoft.Win32.UserPreferenceCategory.VisualStyle) &&
            !ThemeManager.ShouldRefreshFor(Microsoft.Win32.UserPreferenceCategory.Mouse),
            "Theme refresh must react to High Contrast/color changes without refreshing for unrelated input preferences.");

        var model = SettingsControlModel.From(new Settings(), startWithWindows: false, startupManagedByInstaller: false);
        model.HotkeyEnabled = false;
        model.Hotkey = HotkeyBinding.Supported.Single(item => item.SettingValue == "ctrl+shift+e");
        model.Language = UiLanguagePreference.Thai;
        model.Theme = AppThemePreference.Dark;
        model.SkinTone = SkinTonePreference.Dark;
        model.InsertionMode = EmojiInsertMode.Paste;
        model.PasteRestoreDelayMs = 900;
        model.DiagnosticLoggingEnabled = true;
        var settings = model.ToSettings(new Settings());
        Assert(!settings.HotkeyEnabled && settings.ParsedHotkey.SettingValue == "ctrl+shift+e" &&
            settings.LanguagePreference == UiLanguagePreference.Thai &&
            settings.ThemePreference == AppThemePreference.Dark &&
            settings.PreferredSkinTone == SkinTonePreference.Dark &&
            settings.InsertMode == EmojiInsertMode.Paste &&
            settings.PasteRestoreDelayMs == 900 && settings.DiagnosticLoggingEnabled,
            "The single Settings model must commit every user-facing choice together.");

        model.ResetAdvancedDefaults();
        Assert(model.PasteRestoreDelayMs == Settings.DefaultPasteRestoreDelayMs &&
            !model.DiagnosticLoggingEnabled,
            "Reset advanced defaults must reset both delay and diagnostic logging.");
    }

    private static void VerifyPersistenceAndAdvancedReset()
    {
        var root = Path.Combine(Path.GetTempPath(), $"modern-emoji-picker-settings-{Guid.NewGuid():N}");
        var path = Path.Combine(root, "settings.json");
        try
        {
            var settings = new Settings
            {
                HotkeyEnabled = false,
                HotkeyGesture = "ctrl+alt+space",
                UiLanguage = "th",
                Theme = "dark",
                EmojiInsertMode = "keystroke",
                PasteRestoreDelayMs = 99999,
                DiagnosticLoggingEnabled = true,
                WelcomeShown = true,
            };
            settings.SaveTo(path);
            var loaded = Settings.LoadFrom(path);
            Assert(!loaded.HotkeyEnabled && loaded.ParsedHotkey.SettingValue == "ctrl+alt+space" &&
                loaded.LanguagePreference == UiLanguagePreference.Thai &&
                loaded.ThemePreference == AppThemePreference.Dark &&
                loaded.InsertMode == EmojiInsertMode.Keystroke &&
                loaded.PasteRestoreDelayMs == Settings.MaximumPasteRestoreDelayMs &&
                loaded.DiagnosticLoggingEnabled && loaded.WelcomeShown,
                "All Settings and the one-time Welcome marker must persist atomically with validation.");

            loaded.ResetAdvancedDefaults();
            Assert(loaded.PasteRestoreDelayMs == Settings.DefaultPasteRestoreDelayMs &&
                !loaded.DiagnosticLoggingEnabled,
                "Advanced reset must restore privacy-safe defaults.");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
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
