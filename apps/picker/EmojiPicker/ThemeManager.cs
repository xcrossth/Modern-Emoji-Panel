using System;
using System.Windows;
using Microsoft.Win32;

namespace EmojiPicker
{
    /// <summary>
    /// Keeps the application's brushes in step with the Windows light/dark setting,
    /// swapping the merged theme dictionary live when the user changes it.
    /// </summary>
    internal static class ThemeManager
    {
        private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string AppsUseLightThemeValue = "AppsUseLightTheme";

        private static readonly Uri LightThemeUri = new Uri("Theme/LightTheme.xaml", UriKind.Relative);
        private static readonly Uri DarkThemeUri = new Uri("Theme/DarkTheme.xaml", UriKind.Relative);
        private static readonly Uri HighContrastThemeUri = new Uri("Theme/HighContrastTheme.xaml", UriKind.Relative);

        private static ResourceDictionary? currentTheme;
        private static Uri? currentThemeUri;

        /// <summary>
        /// Merges the theme matching the current Windows setting and starts
        /// listening for changes. Call once at startup.
        /// </summary>
        public static void Initialize()
        {
            Apply(ResolveThemeUri(Settings.Current.ThemePreference));
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        internal static void Refresh() => Apply(ResolveThemeUri(Settings.Current.ThemePreference));

        public static void Shutdown()
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }

        private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (!ShouldRefreshFor(e.Category))
            {
                return;
            }

            var themeUri = ResolveThemeUri(Settings.Current.ThemePreference);
            if (themeUri == currentThemeUri)
            {
                return;
            }

            // SystemEvents fires on a background thread; touch resources on the UI
            // thread. Guard the cross-thread call against a racing app shutdown -
            // Invoke throws on this thread once the dispatcher has begun shutting
            // down, and that exception wouldn't be caught by the UI handler.
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
            {
                return;
            }

            try
            {
                dispatcher.Invoke(() => Apply(themeUri));
            }
            catch (Exception)
            {
                // The dispatcher shut down between the check and the Invoke; harmless
            }
        }

        private static void Apply(Uri themeUri)
        {
            var app = Application.Current;
            if (app == null)
            {
                return;
            }

            var theme = new ResourceDictionary { Source = themeUri };

            if (currentTheme != null)
            {
                app.Resources.MergedDictionaries.Remove(currentTheme);
            }

            app.Resources.MergedDictionaries.Add(theme);
            currentTheme = theme;
            currentThemeUri = themeUri;
        }

        private static bool IsSystemDark()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
                // The value is 1 for light apps, 0 for dark; absent means light
                return key?.GetValue(AppsUseLightThemeValue) is int light && light == 0;
            }
            catch (Exception)
            {
                return false; // default to light if the setting can't be read
            }
        }

        internal static bool ResolveDark(AppThemePreference preference, bool? systemDark = null) => preference switch
        {
            AppThemePreference.Dark => true,
            AppThemePreference.Light => false,
            _ => systemDark ?? IsSystemDark(),
        };

        internal static Uri ResolveThemeUri(
            AppThemePreference preference,
            bool? systemDark = null,
            bool? highContrast = null)
        {
            if (highContrast ?? SystemParameters.HighContrast)
            {
                return HighContrastThemeUri;
            }

            return ResolveDark(preference, systemDark) ? DarkThemeUri : LightThemeUri;
        }

        internal static bool ShouldRefreshFor(UserPreferenceCategory category) => category is
            UserPreferenceCategory.Accessibility or
            UserPreferenceCategory.Color or
            UserPreferenceCategory.General or
            UserPreferenceCategory.VisualStyle;
    }
}
