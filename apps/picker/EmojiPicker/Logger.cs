using System;
using System.IO;

namespace EmojiPicker
{
    /// <summary>
    /// Lightweight opt-in file logger for diagnosing runtime issues (the picker
    /// not appearing, hotkey/foreground problems, etc.). Logging is off by default
    /// and controlled through Advanced Settings. The state persists through
    /// Settings. Every write obeys the same opt-in switch; there
    /// is no fatal-error bypass that could create a log without consent.
    /// </summary>
    internal static class Logger
    {
        // Rotate before the log grows unbounded (it's opt-in, but users forget
        // to turn it off); the previous log is kept once as debug.old.log
        private const long MaxLogBytes = 5 * 1024 * 1024;

        private static readonly object Gate = new object();

        private static readonly string Dir = ProductIdentity.DataDirectory;

        public static string LogPath { get; } = Path.Combine(Dir, "debug.log");

        public static bool Enabled { get; private set; }

        /// <summary>Applies the persisted opt-in state. Call after Settings.Load.</summary>
        public static void Initialize(bool enabled)
        {
            Enabled = enabled;
            if (Enabled)
            {
                Log("--- logging resumed (enabled) ---");
            }
        }

        internal static void SetEnabled(bool enabled)
        {
            if (Enabled == enabled)
            {
                return;
            }

            if (Enabled)
            {
                Log("--- logging disabled ---");
            }

            Enabled = enabled;
            if (Enabled)
            {
                Log("--- logging enabled ---");
            }
        }

        public static void Log(string message)
        {
            if (Enabled)
            {
                Write(message);
            }
        }

        /// <summary>Compatibility entry point; still obeys the user's opt-in.</summary>
        public static void LogAlways(string message)
        {
            Log(message);
        }

        private static void Write(string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Dir);

                    var info = new FileInfo(LogPath);
                    if (info.Exists && info.Length > MaxLogBytes)
                    {
                        File.Move(LogPath, Path.ChangeExtension(LogPath, ".old.log"), overwrite: true);
                    }

                    File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}");
                }
            }
            catch (Exception)
            {
                // Logging must never crash the app or interrupt the user
            }
        }
    }
}
