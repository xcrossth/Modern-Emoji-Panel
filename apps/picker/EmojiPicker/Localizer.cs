using System.Globalization;

namespace EmojiPicker;

/// <summary>
/// Resolves the two supported UI locales without affecting the bilingual search
/// index. Unsupported Windows display languages always use English.
/// </summary>
internal static class Localizer
{
    internal static CultureInfo ResolveCulture(
        UiLanguagePreference preference,
        CultureInfo? systemCulture = null)
    {
        var language = preference switch
        {
            UiLanguagePreference.Thai => "th-TH",
            UiLanguagePreference.English => "en-US",
            _ when string.Equals(
                (systemCulture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName,
                "th",
                StringComparison.OrdinalIgnoreCase) => "th-TH",
            _ => "en-US",
        };
        return CultureInfo.GetCultureInfo(language);
    }

    internal static void Apply(UiLanguagePreference preference)
    {
        var culture = ResolveCulture(preference);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    internal static bool IsThai =>
        string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "th", StringComparison.OrdinalIgnoreCase);

    internal static string Text(string english, string thai) => IsThai ? thai : english;
}
