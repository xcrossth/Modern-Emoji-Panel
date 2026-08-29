using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Threading;

namespace EmojiPicker;

public partial class App
{
    private async void RunChromeOmniboxRegressionSmoke(
        string reportPath,
        int requested,
        string insertionMode,
        IntPtr requestedTargetWindow)
    {
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        Localizer.Apply(UiLanguagePreference.English);
        ThemeManager.Initialize();
        Settings.UseTransientForSmoke(new Settings { EmojiInsertMode = insertionMode });

        MainWindow? pickerWindow = null;
        ValuePattern? valuePattern = null;
        string? originalValue = null;
        try
        {
            var target = FindChromeOmnibox(requestedTargetWindow);
            if (target == null)
            {
                throw new InvalidOperationException("Chrome omnibox was not found.");
            }

            var (targetWindow, omnibox, pattern) = target.Value;
            valuePattern = pattern;
            originalValue = pattern.Current.Value;
            pattern.SetValue(string.Empty);
            NativeMethods.SetForegroundWindow(targetWindow);
            omnibox.SetFocus();
            await Task.Delay(100);

            PreviousForegroundWindow = targetWindow;
            PreviousFocusWindow = TextInjector.GetFocusedControl(targetWindow);
            PreviousAccessibilityFocus = AccessibilityFocusSnapshot.Capture(targetWindow);
            PreviousCaretRect = null;

            pickerWindow = new MainWindow(loadUserActivity: false);
            pickerWindow.ShowPicker();
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render);
            // A real click cannot arrive in the same render tick that created the
            // Picker. Let Chrome and WPF finish their activation notifications so
            // the first sample measures insertion rather than an impossible race.
            await Task.Delay(150);
            var whiteHeart = pickerWindow.SmokeEntries.First(entry => entry.Character == "🤍");

            var samples = new List<object>(requested);
            for (var index = 0; index < requested; index++)
            {
                // This command-line smoke is launched without a physical hotkey,
                // so Windows does not grant it the foreground-transfer permission
                // that the resident Picker receives from Win + .. Put the verified
                // target back in front before programmatically committing; focus
                // handoff itself is covered by the desktop regression smoke.
                NativeMethods.SetForegroundWindow(targetWindow);
                omnibox.SetFocus();
                await Task.Delay(25);
                pickerWindow.CommitEmojiForSmoke(whiteHeart);
                var settled = await WaitForChromeInsertionIdleAsync(pickerWindow);
                var actual = pattern.Current.Value;
                var expected = string.Concat(Enumerable.Repeat(whiteHeart.Character, index + 1));
                samples.Add(new
                {
                    index,
                    settled,
                    actualUtf16Length = actual.Length,
                    actualCodeUnits = actual.Select(character => $"U+{(int)character:X4}").ToArray(),
                    targetWindow = targetWindow.ToInt64(),
                    foregroundWindow = NativeMethods.GetForegroundWindow().ToInt64(),
                    errorVisible = pickerWindow.InsertionErrorVisible,
                    errorText = pickerWindow.InsertionErrorTextForSmoke,
                    exact = string.Equals(actual, expected, StringComparison.Ordinal),
                });
            }

            var finalValue = pattern.Current.Value;
            var expectedValue = string.Concat(Enumerable.Repeat(whiteHeart.Character, requested));
            var exactSequence = string.Equals(finalValue, expectedValue, StringComparison.Ordinal);
            var replacementOrUnpairedSurrogate = finalValue.Contains('\uFFFD') ||
                !HasOnlyPairedSurrogates(finalValue);
            var atomicTargetDetected = PreviousAccessibilityFocus?.RequiresAtomicSupplementaryText == true;
            var passed = atomicTargetDetected && exactSequence &&
                !replacementOrUnpairedSurrogate && !pickerWindow.InsertionErrorVisible &&
                pickerWindow.EmojiGridInteractiveForSmoke;
            WriteChromeOmniboxReport(reportPath, new
            {
                schemaVersion = 1,
                requested,
                insertionMode = Settings.Current.InsertMode.ToString(),
                accessibilityFocusCaptured = PreviousAccessibilityFocus != null,
                atomicTargetDetected,
                samples,
                finalCodeUnits = finalValue.Select(character => $"U+{(int)character:X4}").ToArray(),
                exactSequence,
                replacementOrUnpairedSurrogate,
                errorVisible = pickerWindow.InsertionErrorVisible,
                gridInteractive = pickerWindow.EmojiGridInteractiveForSmoke,
                passed,
            });
            Shutdown(passed ? 0 : 1);
        }
        catch (Exception exception)
        {
            WriteChromeOmniboxReport(reportPath, new
            {
                schemaVersion = 1,
                passed = false,
                error = new { type = exception.GetType().Name, exception.Message },
            });
            Shutdown(1);
        }
        finally
        {
            try
            {
                if (valuePattern != null && originalValue != null)
                {
                    valuePattern.SetValue(originalValue);
                }
            }
            catch (ElementNotAvailableException)
            {
                // Chrome closed or rebuilt its omnibox while the smoke was running.
            }
            catch (InvalidOperationException)
            {
                // The provider stopped exposing ValuePattern during shutdown.
            }
            catch (COMException)
            {
                // Chrome's accessibility provider disconnected during shutdown.
            }

            pickerWindow?.PrepareForProcessExit();
            pickerWindow?.Close();
            ThemeManager.Shutdown();
        }
    }

    private static (IntPtr Window, AutomationElement Omnibox, ValuePattern Pattern)? FindChromeOmnibox(
        IntPtr requestedTargetWindow)
    {
        var targetWindow = requestedTargetWindow != IntPtr.Zero
            ? requestedTargetWindow
            : NativeMethods.GetForegroundWindow();
        if (targetWindow == IntPtr.Zero || !NativeMethods.IsWindow(targetWindow))
        {
            return null;
        }

        var targetRoot = AutomationElement.FromHandle(targetWindow);
        var condition = new PropertyCondition(
            AutomationElement.ClassNameProperty,
            "OmniboxViewViews");
        var focused = targetRoot.FindFirst(TreeScope.Descendants, condition);
        if (focused == null)
        {
            return null;
        }

        var current = focused.Current;
        if (!string.Equals(current.FrameworkId, "Chrome", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(current.ClassName, "OmniboxViewViews", StringComparison.Ordinal) ||
            !focused.TryGetCurrentPattern(ValuePattern.Pattern, out var patternObject) ||
            patternObject is not ValuePattern pattern)
        {
            return null;
        }

        return (targetWindow, focused, pattern);
    }

    private static bool HasOnlyPairedSurrogates(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (char.IsHighSurrogate(value[index]))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[++index]))
                {
                    return false;
                }
            }
            else if (char.IsLowSurrogate(value[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<bool> WaitForChromeInsertionIdleAsync(MainWindow window)
    {
        for (var waited = 0; waited < 3000; waited += 25)
        {
            await Task.Delay(25);
            await window.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
            if (window.InsertionIdleForSmoke)
            {
                return true;
            }
        }

        return false;
    }

    private static void WriteChromeOmniboxReport(string reportPath, object report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllText(
            reportPath,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }
}
