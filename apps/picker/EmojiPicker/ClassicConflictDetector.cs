using System;
using System.Threading;

namespace EmojiPicker
{
    /// <summary>
    /// Detects Classic Emoji Picker through its exact named mutex. The probe only
    /// opens and closes a kernel-object handle; it never signals, waits on, kills,
    /// or otherwise changes the Classic process.
    /// </summary>
    internal sealed class ClassicConflictDetector
    {
        private readonly Func<string, bool> mutexExists;

        public ClassicConflictDetector()
            : this(NamedMutexExists)
        {
        }

        internal ClassicConflictDetector(Func<string, bool> mutexExists)
        {
            this.mutexExists = mutexExists;
        }

        public bool IsClassicRunning() => mutexExists(ClassicProductIdentity.MutexName);

        internal static bool NamedMutexExists(string name)
        {
            try
            {
                if (!Mutex.TryOpenExisting(name, out var mutex))
                {
                    return false;
                }

                mutex.Dispose();
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // If the exact object exists but cannot be opened, avoid taking
                // Win+. because another integrity level may own Classic.
                return true;
            }
        }
    }
}
