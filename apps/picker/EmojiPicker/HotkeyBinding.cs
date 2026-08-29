namespace EmojiPicker;

[Flags]
internal enum HotkeyModifiers
{
    None = 0,
    Win = 1,
    Control = 2,
    Alt = 4,
    Shift = 8,
}

/// <summary>
/// A deliberately small, validated hotkey vocabulary. Keeping parsing and the
/// Windows hook representation together prevents an invalid hand-edited setting
/// from disabling the resident app without a safe fallback.
/// </summary>
internal sealed record HotkeyBinding(
    string SettingValue,
    HotkeyModifiers Modifiers,
    int VirtualKey,
    string EnglishDisplayName,
    string ThaiDisplayName)
{
    internal const int VkPeriod = 0xBE;
    internal const int VkSpace = 0x20;
    internal const int VkE = 0x45;

    internal static HotkeyBinding Default => Supported[0];

    internal static IReadOnlyList<HotkeyBinding> Supported { get; } =
    [
        new("win+period", HotkeyModifiers.Win, VkPeriod, "Win + .", "Win + ."),
        new("ctrl+alt+space", HotkeyModifiers.Control | HotkeyModifiers.Alt, VkSpace,
            "Ctrl + Alt + Space", "Ctrl + Alt + Space"),
        new("ctrl+shift+e", HotkeyModifiers.Control | HotkeyModifiers.Shift, VkE,
            "Ctrl + Shift + E", "Ctrl + Shift + E"),
    ];

    internal static HotkeyBinding Parse(string? value) =>
        Supported.FirstOrDefault(binding => string.Equals(
            binding.SettingValue,
            value?.Trim(),
            StringComparison.OrdinalIgnoreCase)) ?? Default;

    internal string GetDisplayName(bool thai) => thai ? ThaiDisplayName : EnglishDisplayName;
}
