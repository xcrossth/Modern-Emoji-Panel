using System;
using System.Windows;
using System.Windows.Threading;
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
        private static DispatcherTimer? systemRefreshTimer;

        /// <summary>
        /// Merges the theme matching the current Windows setting and starts
        /// listening for changes. Call once at startup.
        /// </summary>
        public static void Initialize()
        {
            Apply(ResolveThemeUri(Settings.Current.ThemePreference));
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                systemRefreshTimer = new DispatcherTimer(
                    TimeSpan.FromMilliseconds(100),
                    DispatcherPriority.ApplicationIdle,
                    (_, _) =>
                    {
                        systemRefreshTimer?.Stop();
                        Refresh();
                    },
                    dispatcher);
                systemRefreshTimer.Stop();
            }

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        internal static void Refresh()
        {
            var themeUri = ResolveThemeUri(Settings.Current.ThemePreference);
            if (themeUri != currentThemeUri)
            {
                Apply(themeUri);
            }
        }

        public static void Shutdown()
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            systemRefreshTimer?.Stop();
            systemRefreshTimer = null;
        }

        private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (!ShouldRefreshFor(e.Category))
            {
                return;
            }

            // SystemEvents fires on a background thread and Windows can raise the
            // notification before HighContrast/Personalize values have settled.
            // Coalesce the burst on the UI dispatcher, then resolve the state there.
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
            {
                return;
            }

            try
            {
                dispatcher.BeginInvoke(new Action(() =>
                {
                    if (systemRefreshTimer == null)
                    {
                        Refresh();
                        return;
                    }

                    systemRefreshTimer.Stop();
                    systemRefreshTimer.Start();
                }), DispatcherPriority.ApplicationIdle);
            }
            catch (Exception)
            {
                // The dispatcher shut down between the check and BeginInvoke; harmless
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

        internal static Uri ApplyForSmoke(
            AppThemePreference preference,
            bool systemDark,
            bool highContrast)
        {
            var themeUri = ResolveThemeUri(preference, systemDark, highContrast);
            Apply(themeUri);
            return themeUri;
        }
    }
}
