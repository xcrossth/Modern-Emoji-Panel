using System;
using System.Threading;

namespace EmojiPicker
{
    /// <summary>
    /// Owns the per-user single-instance mutex and the run-again signal. A
    /// secondary launch signals the primary and exits without initializing WPF
    /// user state, the global keyboard hook, or the tray icon.
    /// </summary>
    internal sealed class SingleInstanceCoordinator : IDisposable
    {
        private readonly Mutex instanceMutex;
        private readonly EventWaitHandle showEvent;
        private Thread? listenerThread;
        private volatile bool stopping;
        private bool disposed;

        private SingleInstanceCoordinator(Mutex instanceMutex, EventWaitHandle showEvent)
        {
            this.instanceMutex = instanceMutex;
            this.showEvent = showEvent;
        }

        public event Action? ShowRequested;

        public static bool TryAcquire(
            string mutexName,
            string showEventName,
            out SingleInstanceCoordinator? coordinator)
        {
            // Create/open the event first so a launch racing the primary startup
            // can always latch its signal after observing the mutex.
            var signal = new EventWaitHandle(false, EventResetMode.AutoReset, showEventName);
            var mutex = new Mutex(true, mutexName, out var isNew);
            if (!isNew)
            {
                try
                {
                    signal.Set();
                }
                finally
                {
                    signal.Dispose();
                    mutex.Dispose();
                }

                coordinator = null;
                return false;
            }

            coordinator = new SingleInstanceCoordinator(mutex, signal);
            return true;
        }

        public void StartListening()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (listenerThread != null)
            {
                return;
            }

            listenerThread = new Thread(Listen)
            {
                IsBackground = true,
                Name = "ModernEmojiPicker.RunAgain",
            };
            listenerThread.Start();
        }

        private void Listen()
        {
            while (showEvent.WaitOne())
            {
                if (stopping)
                {
                    return;
                }

                ShowRequested?.Invoke();
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            stopping = true;
            showEvent.Set();
            listenerThread?.Join(1000);
            showEvent.Dispose();

            try
            {
                instanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The owning thread is already ending; closing the handle is enough.
            }

            instanceMutex.Dispose();
        }
    }
}
