using System;
using System.IO;

namespace EmojiPicker
{
    /// <summary>
    /// Stable identity values owned by Modern Emoji Picker. Keep these values
    /// independent from the imported Classic product so both can coexist.
    /// </summary>
    internal static class ProductIdentity
    {
        public const string ProductName = "Modern Emoji Picker";
        public const string Publisher = "X CroSs";
        public const string ExecutableBaseName = "ModernEmojiPicker";
        public const string ExecutableName = ExecutableBaseName + ".exe";
        public const string MutexName = "Local\\XCroSs.ModernEmojiPicker.SingleInstance";
        public const string ShowEventName = "Local\\XCroSs.ModernEmojiPicker.Show";
        public const string RunValueName = "ModernEmojiPicker";
        public const string DataDirectoryName = "ModernEmojiPicker";
        public const string RepositoryUrl = "https://github.com/xcrossth/Modern-Emoji-Panel";

        public static string DataDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            DataDirectoryName);
    }

    /// <summary>
    /// Read-only identifiers used solely to recognize the upstream Classic
    /// product. Modern never opens Classic files or changes Classic state.
    /// </summary>
    internal static class ClassicProductIdentity
    {
        public const string MutexName = "ClassicEmojiPicker.SingleInstance";
        public const string DataDirectoryName = "ClassicEmojiPicker";

        public static string DataDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            DataDirectoryName);
    }
}
