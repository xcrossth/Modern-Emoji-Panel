using System.IO;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace EmojiPicker;

public partial class App
{
    private async void RunDesktopRegressionSmoke(string reportPath)
    {
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        Localizer.Apply(UiLanguagePreference.English);
        ThemeManager.Initialize();
        Settings.ReplaceCurrent(new Settings { EmojiInsertMode = "hybrid" });

        var targetText = new TextBox { AcceptsReturn = true, FontSize = 20 };
        var collapsedEditFallback = new Button { Content = "Collapsed address/search UI fallback" };
        var targetPanel = new StackPanel();
        targetPanel.Children.Add(targetText);
        targetPanel.Children.Add(collapsedEditFallback);
        var target = new System.Windows.Window
        {
            Title = "Modern Picker controlled insertion target",
            Width = 640,
            Height = 240,
            Content = targetPanel,
            ShowInTaskbar = false,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
        };
        MainWindow? pickerWindow = null;
        try
        {
            target.Show();
            target.Activate();
            targetText.Focus();
            System.Windows.Input.Keyboard.Focus(targetText);
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);

            var targetHandle = new WindowInteropHelper(target).Handle;
            PreviousForegroundWindow = targetHandle;
            PreviousFocusWindow = TextInjector.GetFocusedControl(targetHandle);
            PreviousAccessibilityFocus = AccessibilityFocusSnapshot.Capture(targetHandle);
            var accessibilityFocusCaptured = PreviousAccessibilityFocus != null;
            PreviousCaretRect = null;

            pickerWindow = new MainWindow(loadUserActivity: false);
            pickerWindow.ShowPicker();
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render);
            await Task.Delay(100);
            var pickerHandle = new WindowInteropHelper(pickerWindow).Handle;
            var pig = pickerWindow.SmokeEntries.First(entry => entry.Character == "🐖");

            // Chromium/Explorer chrome can collapse its address/search editor when
            // another top-level window activates. Native focus capture then reports
            // only the top-level HWND, so model that state with a non-editable
            // fallback control before asking the current restore path to insert.
            target.Activate();
            collapsedEditFallback.Focus();
            System.Windows.Input.Keyboard.Focus(collapsedEditFallback);
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
            var focusRestoreResult = await TextInjector.TryInsertAsync(
                targetHandle,
                PreviousFocusWindow,
                pig.Character,
                PreviousAccessibilityFocus);
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
            var editableStateRestored = focusRestoreResult.Accepted && targetText.Text == pig.Character;

            targetText.Clear();
            target.Activate();
            targetText.Focus();
            System.Windows.Input.Keyboard.Focus(targetText);
            pickerWindow.Activate();
            NativeMethods.SetForegroundWindow(pickerHandle);
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);

            const int requested = 15;
            for (var index = 0; index < requested; index++)
            {
                // A real pointer click activates the Picker again while an earlier
                // queued insertion may still be settling focus on the target.
                if (!pickerWindow.PointerActivationSuppressedForSmoke)
                {
                    NativeMethods.SetForegroundWindow(pickerHandle);
                }
                pickerWindow.CommitEmojiForSmoke(pig);
                await Task.Delay(4);
            }

            await Task.Delay(1500);
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);

            var expected = string.Concat(Enumerable.Repeat(pig.Character, requested));
            var actual = targetText.Text;
            var exactSequence = string.Equals(actual, expected, StringComparison.Ordinal);
            var replacementOrUnpairedSurrogate = actual.Contains('\uFFFD') ||
                actual.EnumerateRunes().Count() * 2 != actual.Length;
            var errorVisible = pickerWindow.InsertionErrorVisible;
            var gridInteractive = pickerWindow.EmojiGridInteractiveForSmoke;

            pickerWindow.DismissPicker();
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
            var dismissWorked = !pickerWindow.IsVisible;

            var highContrastTheme = ThemeManager.ApplyForSmoke(
                AppThemePreference.System,
                systemDark: true,
                highContrast: true);
            targetText.Clear();
            target.Activate();
            targetText.Focus();
            System.Windows.Input.Keyboard.Focus(targetText);
            pickerWindow.ShowPicker();
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render);
            pickerWindow.DisplaySearchForSmoke("pig");
            pickerWindow.FocusSearchForSmoke();
            var highContrastEnterInputAccepted = TextInjector.SendKeyStrokeForSmoke(
                virtualKey: 0x0D,
                ShortcutModifiers.None);
            var highContrastEnterSettled = await WaitForInsertionIdleAsync(pickerWindow);
            var highContrastEnterWorked = highContrastEnterInputAccepted && highContrastEnterSettled &&
                targetText.Text == pig.Character &&
                !pickerWindow.IsVisible;

            targetText.Clear();
            target.Activate();
            targetText.Focus();
            System.Windows.Input.Keyboard.Focus(targetText);
            pickerWindow.ShowPicker();
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render);
            pickerWindow.DisplaySearchForSmoke("pig");
            pickerWindow.FocusSearchForSmoke();
            var highContrastShiftEnterInputAccepted = TextInjector.SendKeyStrokeForSmoke(
                virtualKey: 0x0D,
                ShortcutModifiers.Shift);
            var highContrastShiftEnterSettled = await WaitForInsertionIdleAsync(pickerWindow);
            var highContrastShiftEnterWorked = highContrastShiftEnterInputAccepted && highContrastShiftEnterSettled &&
                targetText.Text == pig.Character &&
                pickerWindow.IsVisible &&
                pickerWindow.InputMode == PickerInputMode.Search &&
                pickerWindow.EmojiGridInteractiveForSmoke;
            pickerWindow.DismissPicker();

            var highContrastThemeApplied = highContrastTheme.OriginalString.EndsWith(
                "HighContrastTheme.xaml",
                StringComparison.Ordinal);
            var passed = editableStateRestored && exactSequence && !replacementOrUnpairedSurrogate &&
                !errorVisible && gridInteractive && dismissWorked && highContrastThemeApplied &&
                highContrastEnterWorked && highContrastShiftEnterWorked;
            var report = new
            {
                schemaVersion = 1,
                requested,
                expectedUtf16Length = expected.Length,
                actualUtf16Length = actual.Length,
                actualCodeUnits = actual.Select(character => $"U+{(int)character:X4}").ToArray(),
                accessibilityFocusCaptured,
                editableStateRestored,
                exactSequence,
                replacementOrUnpairedSurrogate,
                errorVisible,
                gridInteractive,
                dismissWorked,
                highContrastThemeApplied,
                highContrastEnterWorked,
                highContrastShiftEnterWorked,
                passed,
            };

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllText(
                reportPath,
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Shutdown(passed ? 0 : 1);
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllText(
                reportPath,
                JsonSerializer.Serialize(
                    new { passed = false, error = exception.GetType().Name },
                    new JsonSerializerOptions { WriteIndented = true }));
            Shutdown(1);
        }
        finally
        {
            pickerWindow?.PrepareForProcessExit();
            pickerWindow?.Close();
            target.Close();
            ThemeManager.Shutdown();
        }
    }

    private static async Task<bool> WaitForInsertionIdleAsync(MainWindow window)
    {
        const int maximumWaitMilliseconds = 3000;
        for (var waited = 0; waited < maximumWaitMilliseconds; waited += 25)
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
}
