using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace EmojiPicker
{
    /// <summary>
    /// Types text into the window that was focused before the picker opened,
    /// mimicking the Windows 10 emoji panel's insert behaviour.
    /// </summary>
    internal static class TextInjector
    {
        private static readonly int? CurrentIntegrityRid = GetCurrentIntegrityRid();

        /// <summary>
        /// Returns the control that currently has keyboard focus within
        /// <paramref name="topLevel"/>'s thread (e.g. Explorer's Search or address
        /// edit), or the top-level window itself when it can't be determined.
        /// Captured before the picker opens so focus can be restored on insert.
        /// </summary>
        public static IntPtr GetFocusedControl(IntPtr topLevel)
        {
            if (topLevel == IntPtr.Zero)
            {
                return topLevel;
            }

            var threadId = NativeMethods.GetWindowThreadProcessId(topLevel, out _);
            var gui = new NativeMethods.GUITHREADINFO { cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
            if (threadId != 0 && NativeMethods.GetGUIThreadInfo(threadId, ref gui) && gui.hwndFocus != IntPtr.Zero)
            {
                return gui.hwndFocus;
            }

            return topLevel;
        }

        /// <summary>
        /// Screen-coordinate rectangle of the text caret in <paramref name="topLevel"/>'s
        /// thread. Classic edit controls expose a native caret; rendered Chromium and
        /// Electron controls may expose only an accessibility caret. Returns false when
        /// neither source is usable, so placement can fall back to the target window.
        /// </summary>
        public static bool TryGetCaretRect(IntPtr topLevel, out System.Drawing.Rectangle rect)
        {
            return CaretRectResolver.TryResolve(
                topLevel,
                TryGetNativeCaretRect,
                MsaaCaretCapture.TryGetCaretRect,
                out rect);
        }

        private static bool TryGetNativeCaretRect(IntPtr topLevel, out System.Drawing.Rectangle rect)
        {
            rect = default;
            if (topLevel == IntPtr.Zero)
            {
                return false;
            }

            var threadId = NativeMethods.GetWindowThreadProcessId(topLevel, out _);
            var gui = new NativeMethods.GUITHREADINFO { cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
            if (threadId == 0 || !NativeMethods.GetGUIThreadInfo(threadId, ref gui) || gui.hwndCaret == IntPtr.Zero)
            {
                return false;
            }

            // rcCaret is in hwndCaret's client coordinates; convert both corners
            var topLeft = new System.Drawing.Point(gui.rcCaret.Left, gui.rcCaret.Top);
            var bottomRight = new System.Drawing.Point(gui.rcCaret.Right, gui.rcCaret.Bottom);
            if (!NativeMethods.ClientToScreen(gui.hwndCaret, ref topLeft) || !NativeMethods.ClientToScreen(gui.hwndCaret, ref bottomRight))
            {
                return false;
            }

            rect = System.Drawing.Rectangle.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);

            // A real text caret has a line height. Some apps keep a system caret
            // parked/hidden at client (0,0) with an empty rect even when the visible
            // cursor is elsewhere; treat that as "no caret" so the picker anchors to
            // the mouse instead of the window's top-left corner.
            if (rect.Height <= 0)
            {
                rect = default;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Attempts to focus <paramref name="targetWindow"/> and type <paramref name="text"/> into it.
        /// <paramref name="focusWindow"/> is the child control that had keyboard focus before the
        /// picker opened; focus is restored to it so text lands in the right place. The result
        /// distinguishes accepted input from a safe abort; the caller must not retry or retarget.
        /// Must be awaited on the UI thread; the focus-settle delay is non-blocking.
        /// </summary>
        public static async Task<InsertionResult> TryInsertAsync(IntPtr targetWindow, IntPtr focusWindow, string text)
        {
            var activationFailure = await TryActivateValidatedTargetAsync(targetWindow, focusWindow);
            if (activationFailure != null)
            {
                return activationFailure;
            }

            var method = InsertionPolicy.SelectMethod(Settings.Current.InsertMode, text);
            if (method == InsertionMethod.TemporaryPaste)
            {
                return await PasteViaClipboardAsync(text);
            }

            return SendUnicodeKeystrokes(text);
        }

        /// <summary>
        /// Replays one captured physical key and its modifiers only after restoring
        /// and revalidating the exact pre-picker target. It is held only in memory and is never
        /// logged, persisted or translated through the clipboard.
        /// </summary>
        internal static async Task<InsertionResult> TrySendKeyStrokeAsync(
            IntPtr targetWindow,
            IntPtr focusWindow,
            ushort virtualKey,
            ShortcutModifiers modifiers)
        {
            if (virtualKey == 0)
            {
                return InsertionResult.Failure("The key handoff was invalid.");
            }

            var activationFailure = await TryActivateValidatedTargetAsync(targetWindow, focusWindow);
            if (activationFailure != null)
            {
                return activationFailure;
            }

            return SendKeyStroke(virtualKey, modifiers);
        }

        private static async Task<InsertionResult?> TryActivateValidatedTargetAsync(
            IntPtr targetWindow,
            IntPtr focusWindow)
        {
            if (targetWindow == IntPtr.Zero || !NativeMethods.IsWindow(targetWindow))
            {
                return InsertionResult.Failure(
                    "The original target is no longer available.",
                    targetWindow == IntPtr.Zero ? TargetValidationFailure.MissingTarget : TargetValidationFailure.TargetClosed);
            }

            if (!NativeMethods.SetForegroundWindow(targetWindow))
            {
                return InsertionResult.Failure("Windows did not activate the original target.");
            }

            // Restore focus to the exact control that had it; activating our picker
            // moves focus off edits like Explorer's Search box or address bar.
            RestoreFocus(targetWindow, focusWindow);

            // Wait for the target to actually become foreground before injecting,
            // then give keyboard focus one additional dispatcher-independent tick.
            var waited = 0;
            while (waited < 250 && NativeMethods.GetForegroundWindow() != targetWindow)
            {
                await Task.Delay(15);
                waited += 15;
            }

            await Task.Delay(15);
            Logger.Log($"Insert: target foreground after ~{waited}ms");

            var targetExists = NativeMethods.IsWindow(targetWindow);
            var targetIntegrityRid = targetExists ? GetWindowIntegrityRid(targetWindow) : null;
            // Keep this read immediately adjacent to validation and injection: a
            // window that became foreground after activation must never be used as
            // a replacement target.
            var foregroundTarget = NativeMethods.GetForegroundWindow();
            var validation = TargetValidationPolicy.Validate(
                targetWindow,
                targetExists,
                foregroundTarget,
                CurrentIntegrityRid,
                targetIntegrityRid);
            if (validation == TargetValidationFailure.None)
            {
                return null;
            }

            Logger.Log($"Insert aborted by target validation: {validation}");
            return InsertionResult.Failure(TargetFailureMessage(validation), validation);
        }

        /// <summary>
        /// Returns keyboard focus to the exact app/control captured before the Picker
        /// Session. This is used only by explicit dismissal gestures; an outside click
        /// deliberately does not call it, so the window chosen by the user keeps focus.
        /// </summary>
        internal static bool TryRestoreCapturedTarget(IntPtr targetWindow, IntPtr focusWindow)
        {
            if (targetWindow == IntPtr.Zero || !NativeMethods.IsWindow(targetWindow) ||
                !NativeMethods.SetForegroundWindow(targetWindow))
            {
                return false;
            }

            RestoreFocus(targetWindow, focusWindow);
            return true;
        }

        private static InsertionResult SendUnicodeKeystrokes(string text)
        {
            // All key-downs first, then all key-ups: the two halves of a surrogate
            // pair must produce consecutive WM_CHAR messages or the receiving edit
            // control shows two broken characters instead of one emoji
            var inputs = new NativeMethods.INPUT[text.Length * 2];
            for (int i = 0; i < text.Length; i++)
            {
                inputs[i] = UnicodeKeyEvent(text[i], keyUp: false);
                inputs[text.Length + i] = UnicodeKeyEvent(text[i], keyUp: true);
            }

            var accepted = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
            return accepted == (uint)inputs.Length
                ? new InsertionResult(true, InsertionMethod.UnicodeKeystrokes, null,
                    AcceptedInputCount: accepted, RequestedInputCount: (uint)inputs.Length)
                : new InsertionResult(false, InsertionMethod.UnicodeKeystrokes,
                    $"Windows accepted {accepted} of {inputs.Length} Unicode input events. The operation was not retried.",
                    AcceptedInputCount: accepted, RequestedInputCount: (uint)inputs.Length);
        }

        /// <summary>
        /// Places <paramref name="text"/> on the clipboard, sends Ctrl+V so the
        /// target composes it as one string (which synthetic keystrokes fail to do
        /// for joined emoji in some apps), then restores the previous clipboard
        /// content. The full clipboard (all formats, incl. images/files) is
        /// snapshotted and restored, and both writes are tagged to stay out of
        /// Clipboard History (Win+V), Cloud Clipboard and third-party monitors, so
        /// the transient paste neither destroys existing content nor pollutes the
        /// history stack.
        /// </summary>
        private static async Task<InsertionResult> PasteViaClipboardAsync(string text)
        {
            // Snapshot the ENTIRE clipboard (all formats) before overwriting, so a
            // copied image / file selection isn't destroyed by the paste. Grab the
            // text separately as a guaranteed-restorable fallback.
            var previous = CaptureClipboard();

            if (!SetClipboardTextExcluded(text))
            {
                return new InsertionResult(false, InsertionMethod.TemporaryPaste,
                    "The temporary clipboard content could not be created.");
            }

            var sequenceAfterSet = NativeMethods.GetClipboardSequenceNumber();
            var acceptedPasteInputs = SendCtrlV();

            // Give the target time to read the clipboard before restoring. A single
            // fixed delay races slow/remote (RDP/Citrix) targets - which then paste
            // the restored old content instead of the emoji - so it is configurable.
            var delay = Math.Clamp(Settings.Current.PasteRestoreDelayMs, 50, 5000);
            await Task.Delay(delay);

            // If the clipboard changed during the wait, the user copied something
            // else - don't clobber their new content with the old snapshot.
            var currentSequence = NativeMethods.GetClipboardSequenceNumber();
            var shouldRestore = ClipboardRestorePolicy.ShouldRestore(previous.Captured, sequenceAfterSet, currentSequence);
            var userCopied = currentSequence != sequenceAfterSet;

            if (shouldRestore)
            {
                // Restore the full snapshot; if that fails (a stale handle-backed
                // format can't be re-serialised), at least put the text back so we
                // never lose more than the old text-only restore did.
                if (!RestoreClipboard(previous.Data) && previous.Text != null)
                {
                    SetClipboardTextExcluded(previous.Text);
                }
            }
            // else: clipboard was empty/unreadable, or the user copied during the
            // wait - leave what's there (the emoji, or the user's new copy).

            Logger.Log($"Paste: inserted via clipboard (Ctrl+V), restore after {delay}ms" +
                (userCopied ? " (skipped - user copied)" : string.Empty));
            return acceptedPasteInputs == 4
                ? new InsertionResult(true, InsertionMethod.TemporaryPaste, null,
                    AcceptedInputCount: acceptedPasteInputs, RequestedInputCount: 4)
                : new InsertionResult(false, InsertionMethod.TemporaryPaste,
                    $"Windows accepted {acceptedPasteInputs} of 4 paste input events. The operation was not retried.",
                    AcceptedInputCount: acceptedPasteInputs, RequestedInputCount: 4);
        }

        /// <summary>
        /// Copies by explicit user request. Unlike Temporary Paste this deliberately
        /// has no history/cloud exclusion marker and therefore appears in Win+V.
        /// </summary>
        public static bool CopyExplicit(string text)
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Explicit copy failed ({ex.GetType().Name})");
                return false;
            }
        }

        /// <summary>
        /// Puts temporary text on the clipboard tagged so Clipboard History (Win+V),
        /// Cloud Clipboard, and clipboard monitors can ignore it. Returns false on
        /// failure. Explicit user copy must use <see cref="CopyExplicit"/> instead.
        /// </summary>
        public static bool SetClipboardTextExcluded(string text)
        {
            try
            {
                var data = new System.Windows.DataObject();
                data.SetText(text);
                AddHistoryExclusion(data);
                System.Windows.Clipboard.SetDataObject(data, copy: true);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Clipboard set failed ({ex.GetType().Name})");
                return false;
            }
        }

        /// <summary>
        /// Copies every native clipboard format into a detached DataObject while the
        /// clipboard is still intact, so it can be restored after the paste. Empty
        /// clipboard is captured explicitly; unreadable clipboard is marked unsafe
        /// to restore so newer data can never be overwritten.
        /// </summary>
        private static ClipboardCapture CaptureClipboard()
        {
            try
            {
                var current = System.Windows.Clipboard.GetDataObject();
                if (current == null)
                {
                    return new ClipboardCapture(true, null, null);
                }

                var snapshot = new System.Windows.DataObject();
                var copied = false;
                var formats = current.GetFormats(autoConvert: false);
                foreach (var format in formats)
                {
                    try
                    {
                        var data = current.GetData(format, autoConvert: false);
                        if (data != null)
                        {
                            snapshot.SetData(format, data);
                            copied = true;
                        }
                    }
                    catch (Exception)
                    {
                        // Skip any format that can't be read/round-tripped
                    }
                }

                if (!copied && formats.Length > 0)
                {
                    return new ClipboardCapture(false, null, null);
                }

                return new ClipboardCapture(true, copied ? snapshot : null, TryGetText(snapshot));
            }
            catch (Exception)
            {
                return new ClipboardCapture(false, null, null);
            }
        }

        private static string? TryGetText(System.Windows.IDataObject? snapshot)
        {
            try
            {
                if (snapshot != null && snapshot.GetDataPresent(System.Windows.DataFormats.UnicodeText))
                {
                    return snapshot.GetData(System.Windows.DataFormats.UnicodeText) as string;
                }
            }
            catch (Exception)
            {
                // No usable text; the fallback simply won't run
            }

            return null;
        }

        /// <summary>
        /// Restores a captured snapshot to the clipboard. Returns false if the whole
        /// restore failed (e.g. a handle-backed format went stale), so the caller can
        /// fall back to restoring the plain text.
        /// </summary>
        private static bool RestoreClipboard(System.Windows.IDataObject? snapshot)
        {
            try
            {
                if (snapshot == null)
                {
                    System.Windows.Clipboard.Clear();
                    return true;
                }

                if (snapshot is System.Windows.DataObject data)
                {
                    // History-exclude the restore too, so putting the user's own
                    // content back doesn't add a duplicate Win+V entry.
                    AddHistoryExclusion(data);
                    System.Windows.Clipboard.SetDataObject(data, copy: true);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Log($"Paste: full clipboard restore failed ({ex.GetType().Name}); trying text");
                return false;
            }
        }

        /// <summary>
        /// Tags a DataObject so Clipboard History, Cloud Clipboard, and clipboard
        /// monitors ignore it. Value 0 (DWORD) = exclude.
        /// </summary>
        private static void AddHistoryExclusion(System.Windows.DataObject data)
        {
            var excludeDword = new byte[] { 0, 0, 0, 0 };
            data.SetData("CanIncludeInClipboardHistory", new MemoryStream(excludeDword));
            data.SetData("CanUploadToCloudClipboard", new MemoryStream(excludeDword));
            data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(excludeDword));
        }

        private sealed record ClipboardCapture(
            bool Captured,
            System.Windows.IDataObject? Data,
            string? Text);

        private static uint SendCtrlV()
        {
            var inputs = new[]
            {
                VirtualKeyEvent(VkControl, keyUp: false),
                VirtualKeyEvent(VkV, keyUp: false),
                VirtualKeyEvent(VkV, keyUp: true),
                VirtualKeyEvent(VkControl, keyUp: true),
            };
            return NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        }

        private const ushort VkControl = 0x11;
        private const ushort VkAlt = 0x12;
        private const ushort VkShift = 0x10;
        private const ushort VkLeftWindows = 0x5B;
        private const ushort VkV = 0x56;

        private static InsertionResult SendKeyStroke(ushort virtualKey, ShortcutModifiers modifiers)
        {
            var modifierKeys = new List<ushort>(capacity: 4);
            AddModifier(ShortcutModifiers.Control, VkControl);
            AddModifier(ShortcutModifiers.Alt, VkAlt);
            AddModifier(ShortcutModifiers.Shift, VkShift);
            AddModifier(ShortcutModifiers.Windows, VkLeftWindows);

            var inputs = new List<NativeMethods.INPUT>((modifierKeys.Count * 2) + 2);
            inputs.AddRange(modifierKeys.Select(key => VirtualKeyEvent(key, keyUp: false)));
            inputs.Add(VirtualKeyEvent(virtualKey, keyUp: false));
            inputs.Add(VirtualKeyEvent(virtualKey, keyUp: true));
            for (var index = modifierKeys.Count - 1; index >= 0; index--)
            {
                inputs.Add(VirtualKeyEvent(modifierKeys[index], keyUp: true));
            }

            var inputArray = inputs.ToArray();
            var accepted = NativeMethods.SendInput(
                (uint)inputArray.Length,
                inputArray,
                Marshal.SizeOf<NativeMethods.INPUT>());
            return accepted == (uint)inputArray.Length
                ? new InsertionResult(
                    true,
                    InsertionMethod.ShortcutKeystrokes,
                    null,
                    AcceptedInputCount: accepted,
                    RequestedInputCount: (uint)inputArray.Length)
                : new InsertionResult(
                    false,
                    InsertionMethod.ShortcutKeystrokes,
                    $"Windows accepted {accepted} of {inputArray.Length} key handoff events. The operation was not retried.",
                    AcceptedInputCount: accepted,
                    RequestedInputCount: (uint)inputArray.Length);

            void AddModifier(ShortcutModifiers modifier, ushort key)
            {
                if ((modifiers & modifier) != 0)
                {
                    modifierKeys.Add(key);
                }
            }
        }

        private static NativeMethods.INPUT VirtualKeyEvent(ushort virtualKey, bool keyUp)
        {
            return new NativeMethods.INPUT
            {
                type = NativeMethods.InputKeyboard,
                u = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = virtualKey,
                        dwFlags = keyUp ? NativeMethods.KeyEventKeyUp : 0,
                    },
                },
            };
        }

        private static void RestoreFocus(IntPtr targetWindow, IntPtr focusWindow)
        {
            if (focusWindow == IntPtr.Zero || focusWindow == targetWindow || !NativeMethods.IsWindow(focusWindow))
            {
                return;
            }

            var targetThread = NativeMethods.GetWindowThreadProcessId(targetWindow, out _);
            var thisThread = NativeMethods.GetCurrentThreadId();

            // Focus is per input-queue; attach to the target thread so SetFocus takes
            if (targetThread != 0 && targetThread != thisThread && NativeMethods.AttachThreadInput(thisThread, targetThread, true))
            {
                NativeMethods.SetFocus(focusWindow);
                NativeMethods.AttachThreadInput(thisThread, targetThread, false);
            }
            else
            {
                NativeMethods.SetFocus(focusWindow);
            }
        }

        private static int? GetCurrentIntegrityRid()
        {
            var process = NativeMethods.GetCurrentProcess();
            if (!NativeMethods.OpenProcessToken(process, NativeMethods.TokenQuery, out var token))
            {
                return null;
            }

            try
            {
                return GetTokenIntegrityRid(token);
            }
            finally
            {
                NativeMethods.CloseHandle(token);
            }
        }

        /// <summary>
        /// Returns the target process integrity RID used for an exact UIPI
        /// comparison, or null when the process cannot be queried safely.
        /// </summary>
        private static int? GetWindowIntegrityRid(IntPtr window)
        {
            NativeMethods.GetWindowThreadProcessId(window, out var pid);
            if (pid == 0)
            {
                return null;
            }

            var process = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, pid);
            if (process == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                if (!NativeMethods.OpenProcessToken(process, NativeMethods.TokenQuery, out var token))
                {
                    return null;
                }

                try
                {
                    return GetTokenIntegrityRid(token);
                }
                finally
                {
                    NativeMethods.CloseHandle(token);
                }
            }
            finally
            {
                NativeMethods.CloseHandle(process);
            }
        }

        private static int? GetTokenIntegrityRid(IntPtr token)
        {
            NativeMethods.GetTokenInformation(token, NativeMethods.TokenIntegrityLevel, IntPtr.Zero, 0, out var required);
            if (required <= 0 || Marshal.GetLastWin32Error() != NativeMethods.ErrorInsufficientBuffer)
            {
                return null;
            }

            var buffer = Marshal.AllocHGlobal(required);
            try
            {
                if (!NativeMethods.GetTokenInformation(token, NativeMethods.TokenIntegrityLevel, buffer, required, out _))
                {
                    return null;
                }

                // TOKEN_MANDATORY_LABEL begins with SID_AND_ATTRIBUTES; the SID
                // pointer is the first native-sized field.
                var sid = Marshal.ReadIntPtr(buffer);
                var countPointer = NativeMethods.GetSidSubAuthorityCount(sid);
                if (countPointer == IntPtr.Zero)
                {
                    return null;
                }

                var count = Marshal.ReadByte(countPointer);
                if (count == 0)
                {
                    return null;
                }

                var ridPointer = NativeMethods.GetSidSubAuthority(sid, (uint)(count - 1));
                return ridPointer == IntPtr.Zero ? null : Marshal.ReadInt32(ridPointer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static string TargetFailureMessage(TargetValidationFailure failure) => failure switch
        {
            TargetValidationFailure.MissingTarget => "No original target was captured. Choose Copy instead.",
            TargetValidationFailure.TargetClosed => "The original target was closed. Choose Copy instead.",
            TargetValidationFailure.ForegroundChanged => "Focus moved away from the original target, so nothing was sent.",
            TargetValidationFailure.HigherIntegrity => "The original target has a higher integrity level, so Windows blocks input.",
            TargetValidationFailure.IntegrityUnknown => "The original target could not be validated safely.",
            _ => "The original target could not be validated safely.",
        };

        private static NativeMethods.INPUT UnicodeKeyEvent(char codeUnit, bool keyUp)
        {
            return new NativeMethods.INPUT
            {
                type = NativeMethods.InputKeyboard,
                u = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wScan = codeUnit,
                        dwFlags = NativeMethods.KeyEventUnicode | (keyUp ? NativeMethods.KeyEventKeyUp : 0),
                    },
                },
            };
        }
    }
}
