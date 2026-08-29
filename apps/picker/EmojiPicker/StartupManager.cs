using Microsoft.Win32;

namespace EmojiPicker;

/// <summary>
/// Owns Modern's autostart identity. A portable build never calls SetEnabled by
/// itself. An installer may create the machine/per-user Run value during setup;
/// a machine value is surfaced as managed instead of being silently overwritten.
/// </summary>
internal sealed class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    internal bool IsUserEnabled => HasValue(Registry.CurrentUser);

    internal bool IsInstallerManaged => HasValue(Registry.LocalMachine);

    internal bool IsEffectiveEnabled => IsInstallerManaged || IsUserEnabled;

    internal void SetUserEnabled(bool enabled, string? executablePath = null)
    {
        if (IsInstallerManaged)
        {
            return;
        }

        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("The Windows startup registry key is unavailable.");
        if (enabled)
        {
            var path = executablePath ?? Environment.ProcessPath
                ?? throw new InvalidOperationException("The executable path is unavailable.");
            key.SetValue(ProductIdentity.RunValueName, $"\"{path}\"");
        }
        else
        {
            key.DeleteValue(ProductIdentity.RunValueName, throwOnMissingValue: false);
        }
    }

    private static bool HasValue(RegistryKey root)
    {
        try
        {
            using var key = root.OpenSubKey(RunKeyPath);
            return key?.GetValue(ProductIdentity.RunValueName) != null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
