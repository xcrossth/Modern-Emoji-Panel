using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EmojiPicker
{
    internal enum UiLanguagePreference
    {
        System,
        English,
        Thai,
    }

    internal enum AppThemePreference
    {
        System,
        Light,
        Dark,
    }

    /// <summary>How the chosen emoji is delivered to the target app.</summary>
    internal enum EmojiInsertMode
    {
        /// <summary>Type simple emoji; paste joined ones (ZWJ/flag/skin-tone).</summary>
        Hybrid,

        /// <summary>Always paste via the clipboard (Ctrl+V).</summary>
        Paste,

        /// <summary>Always type via synthetic keystrokes; never touch the clipboard.</summary>
        Keystroke,
    }

    /// <summary>
    /// User settings persisted as JSON in %APPDATA%\ModernEmojiPicker\settings.json,
    /// alongside recent.json and the debug log. Missing or unreadable settings fall
    /// back to defaults; a default file is written on first run so it is easy to find
    /// and edit by hand.
    /// </summary>
    internal sealed class Settings
    {
        internal const int CurrentSchemaVersion = 1;
        internal const int DefaultPasteRestoreDelayMs = 250;
        internal const int MinimumPasteRestoreDelayMs = 50;
        internal const int MaximumPasteRestoreDelayMs = 5000;

        private static readonly string Dir = ProductIdentity.DataDirectory;

        private static readonly string FilePath = Path.Combine(Dir, "settings.json");

        public static Settings Current { get; private set; } = new Settings();

        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        [JsonPropertyName("hotkeyEnabled")]
        public bool HotkeyEnabled { get; set; } = true;

        [JsonPropertyName("hotkeyGesture")]
        public string HotkeyGesture { get; set; } = HotkeyBinding.Default.SettingValue;

        [JsonPropertyName("uiLanguage")]
        public string UiLanguage { get; set; } = "system";

        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "system";

        [JsonPropertyName("diagnosticLoggingEnabled")]
        public bool DiagnosticLoggingEnabled { get; set; }

        [JsonPropertyName("welcomeShown")]
        public bool WelcomeShown { get; set; }

        /// <summary>
        /// How emoji are inserted: "hybrid" (default) types simple emoji and pastes
        /// joined ones that synthetic keystrokes split in some apps (ZWJ sequences,
        /// flags, skin-tone variants); "paste" always uses the clipboard; "keystroke"
        /// always types and never touches the clipboard.
        /// </summary>
        [JsonPropertyName("emojiInsertMode")]
        public string EmojiInsertMode { get; set; } = "hybrid";

        /// <summary>
        /// How long (ms) to wait after Ctrl+V before restoring the previous
        /// clipboard, when a joined emoji is pasted. The target reads the clipboard
        /// on its own schedule; a too-short wait can restore the old content before
        /// a slow/remote (RDP/Citrix) target has read the emoji, so it is
        /// configurable. Clamped to 50-5000 ms at use.
        /// </summary>
        [JsonPropertyName("pasteRestoreDelayMs")]
        public int PasteRestoreDelayMs { get; set; } = DefaultPasteRestoreDelayMs;

        /// <summary>
        /// Global skin tone applied to every Emoji Entry that supports a
        /// modifier. This is deliberately independent from one-shot mixed-tone
        /// Variant Overrides.
        /// </summary>
        [JsonPropertyName("globalSkinTone")]
        public string GlobalSkinTone { get; set; } = "neutral";

        /// <summary>Last user-selected Picker size in device-independent pixels.</summary>
        [JsonPropertyName("pickerWidth")]
        public double PickerWidth { get; set; } = 400;

        [JsonPropertyName("pickerHeight")]
        public double PickerHeight { get; set; } = 440;

        [JsonIgnore]
        public EmojiInsertMode InsertMode => EmojiInsertMode?.Trim().ToLowerInvariant() switch
        {
            "paste" => EmojiPicker.EmojiInsertMode.Paste,
            "keystroke" => EmojiPicker.EmojiInsertMode.Keystroke,
            _ => EmojiPicker.EmojiInsertMode.Hybrid,
        };

        [JsonIgnore]
        public SkinTonePreference PreferredSkinTone =>
            SkinTonePreferenceNames.ParseSettingValue(GlobalSkinTone);

        [JsonIgnore]
        public UiLanguagePreference LanguagePreference => UiLanguage?.Trim().ToLowerInvariant() switch
        {
            "th" => UiLanguagePreference.Thai,
            "en" => UiLanguagePreference.English,
            _ => UiLanguagePreference.System,
        };

        [JsonIgnore]
        public AppThemePreference ThemePreference => Theme?.Trim().ToLowerInvariant() switch
        {
            "light" => AppThemePreference.Light,
            "dark" => AppThemePreference.Dark,
            _ => AppThemePreference.System,
        };

        [JsonIgnore]
        public HotkeyBinding ParsedHotkey => HotkeyBinding.Parse(HotkeyGesture);

        /// <summary>Reads the settings file (writing defaults if absent). Call once at startup.</summary>
        public static void Load()
        {
            var fileExists = File.Exists(FilePath);
            Current = LoadFrom(FilePath);
            if (!fileExists)
            {
                Save(); // create a default file users can discover and edit
            }
        }

        internal static void ReplaceCurrent(Settings settings)
        {
            Current = settings.Normalize();
            Save();
        }

        internal Settings Normalize()
        {
            SchemaVersion = CurrentSchemaVersion;
            HotkeyGesture = HotkeyBinding.Parse(HotkeyGesture).SettingValue;
            UiLanguage = LanguagePreference switch
            {
                UiLanguagePreference.Thai => "th",
                UiLanguagePreference.English => "en",
                _ => "system",
            };
            Theme = ThemePreference.ToString().ToLowerInvariant();
            EmojiInsertMode = InsertMode switch
            {
                EmojiPicker.EmojiInsertMode.Paste => "paste",
                EmojiPicker.EmojiInsertMode.Keystroke => "keystroke",
                _ => "hybrid",
            };
            GlobalSkinTone = PreferredSkinTone.ToSettingValue();
            PasteRestoreDelayMs = Math.Clamp(
                PasteRestoreDelayMs,
                MinimumPasteRestoreDelayMs,
                MaximumPasteRestoreDelayMs);
            PickerWidth = Math.Clamp(PickerWidth, 320, 900);
            PickerHeight = Math.Clamp(PickerHeight, 360, 900);
            return this;
        }

        internal void ResetAdvancedDefaults()
        {
            PasteRestoreDelayMs = DefaultPasteRestoreDelayMs;
            DiagnosticLoggingEnabled = false;
        }

        public static void SetGlobalSkinTone(SkinTonePreference preference)
        {
            Current.GlobalSkinTone = preference.ToSettingValue();
            Save();
        }

        public static void SetPickerSize(double width, double height)
        {
            Current.PickerWidth = Math.Clamp(width, 320, 900);
            Current.PickerHeight = Math.Clamp(height, 360, 900);
            Save();
        }

        internal static Settings LoadFrom(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new Settings();
                }

                return (JsonSerializer.Deserialize<Settings>(File.ReadAllText(filePath)) ?? new Settings()).Normalize();
            }
            catch (Exception)
            {
                return new Settings(); // any problem -> safe defaults
            }
        }

        internal void SaveTo(string filePath)
        {
            Normalize();
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            var temporaryPath = filePath + ".tmp";
            File.WriteAllText(temporaryPath, json);
            if (File.Exists(filePath))
            {
                File.Replace(temporaryPath, filePath, null);
            }
            else
            {
                File.Move(temporaryPath, filePath);
            }
        }

        private static void Save()
        {
            try
            {
                Current.SaveTo(FilePath);
            }
            catch (Exception)
            {
                // Writing settings is best-effort; never interrupt the picker
            }
        }
    }
}
