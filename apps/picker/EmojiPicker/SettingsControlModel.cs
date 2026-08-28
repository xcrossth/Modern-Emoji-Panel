namespace EmojiPicker;

/// <summary>
/// One UI-facing settings model. The WPF window edits this detached snapshot and
/// commits it as one validated unit, so individual controls cannot leave runtime
/// state and settings.json out of sync.
/// </summary>
internal sealed class SettingsControlModel
{
    internal bool HotkeyEnabled { get; set; }
    internal HotkeyBinding Hotkey { get; set; } = HotkeyBinding.Default;
    internal bool StartWithWindows { get; set; }
    internal bool StartupManagedByInstaller { get; set; }
    internal UiLanguagePreference Language { get; set; }
    internal AppThemePreference Theme { get; set; }
    internal SkinTonePreference SkinTone { get; set; }
    internal EmojiInsertMode InsertionMode { get; set; }
    internal int PasteRestoreDelayMs { get; set; }
    internal bool DiagnosticLoggingEnabled { get; set; }

    internal static SettingsControlModel From(
        Settings settings,
        bool startWithWindows,
        bool startupManagedByInstaller) => new()
        {
            HotkeyEnabled = settings.HotkeyEnabled,
            Hotkey = settings.ParsedHotkey,
            StartWithWindows = startWithWindows,
            StartupManagedByInstaller = startupManagedByInstaller,
            Language = settings.LanguagePreference,
            Theme = settings.ThemePreference,
            SkinTone = settings.PreferredSkinTone,
            InsertionMode = settings.InsertMode,
            PasteRestoreDelayMs = settings.PasteRestoreDelayMs,
            DiagnosticLoggingEnabled = settings.DiagnosticLoggingEnabled,
        };

    internal Settings ToSettings(Settings existing)
    {
        existing.HotkeyEnabled = HotkeyEnabled;
        existing.HotkeyGesture = Hotkey.SettingValue;
        existing.UiLanguage = Language switch
        {
            UiLanguagePreference.Thai => "th",
            UiLanguagePreference.English => "en",
            _ => "system",
        };
        existing.Theme = Theme.ToString().ToLowerInvariant();
        existing.GlobalSkinTone = SkinTone.ToSettingValue();
        existing.EmojiInsertMode = InsertionMode switch
        {
            EmojiInsertMode.Paste => "paste",
            EmojiInsertMode.Keystroke => "keystroke",
            _ => "hybrid",
        };
        existing.PasteRestoreDelayMs = Math.Clamp(
            PasteRestoreDelayMs,
            Settings.MinimumPasteRestoreDelayMs,
            Settings.MaximumPasteRestoreDelayMs);
        existing.DiagnosticLoggingEnabled = DiagnosticLoggingEnabled;
        return existing.Normalize();
    }

    internal void ResetAdvancedDefaults()
    {
        PasteRestoreDelayMs = Settings.DefaultPasteRestoreDelayMs;
        DiagnosticLoggingEnabled = false;
    }
}
