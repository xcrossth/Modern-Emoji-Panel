using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Interop;
using System.Windows.Threading;

namespace EmojiPicker;

public partial class App
{
    private const double GlobalHotkeySmokeBudgetMilliseconds = 100;
    private const int GlobalHotkeySmokeSamples = 20;

    /// <summary>
    /// Exercises the real low-level keyboard hook and foreground-capture path against
    /// a window launched by the qualification script. It uses transient activity,
    /// never creates the tray, never loads user settings and never inserts text.
    /// </summary>
    private void RunGlobalHotkeySmoke(string reportPath, IntPtr expectedTarget)
    {
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        Localizer.Apply(UiLanguagePreference.English);
        ThemeManager.Initialize();

        var pickerWindow = new MainWindow(loadUserActivity: false);
        var hotkey = new HotkeyListener();
        var injectionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
        var timeoutTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        var completed = false;
        var injectedAt = 0L;
        var sentInputCount = 0U;
        var totalSentInputCount = 0U;
        var foregroundBeforeInjection = IntPtr.Zero;
        var latencies = new List<double>(GlobalHotkeySmokeSamples);
        var hookDispatchLatencies = new List<double>(GlobalHotkeySmokeSamples);
        var targetCaptureLatencies = new List<double>(GlobalHotkeySmokeSamples);
        var showPickerLatencies = new List<double>(GlobalHotkeySmokeSamples);
        var showToRenderLatencies = new List<double>(GlobalHotkeySmokeSamples);
        var resetLatencies = new List<double>(GlobalHotkeySmokeSamples);
        var categoryLatencies = new List<double>(GlobalHotkeySmokeSamples);
        var positionLatencies = new List<double>(GlobalHotkeySmokeSamples);
        var showLatencies = new List<double>(GlobalHotkeySmokeSamples);
        var activateLatencies = new List<double>(GlobalHotkeySmokeSamples);
        var focusLatencies = new List<double>(GlobalHotkeySmokeSamples);
        var categoryCacheReused = false;
        var searchInvalidatedCategoryCache = false;
        var categoryCacheReusedAfterSearch = false;

        void Finish(
            bool passed,
            string? error = null,
            IntPtr capturedTarget = default,
            IntPtr capturedFocus = default,
            bool caretAvailable = false,
            IntPtr pickerHandle = default,
            bool pickerVisible = false,
            bool pickerForeground = false)
        {
            if (completed)
            {
                return;
            }

            completed = true;
            injectionTimer.Stop();
            timeoutTimer.Stop();
            var hookWasInstalled = hotkey.IsActive;
            hotkey.Dispose();

            var targetProcessId = 0U;
            if (NativeMethods.IsWindow(expectedTarget))
            {
                NativeMethods.GetWindowThreadProcessId(expectedTarget, out targetProcessId);
            }

            var orderedLatencies = latencies.OrderBy(value => value).ToArray();
            var medianMilliseconds = Percentile(orderedLatencies, 0.5);
            var p95Milliseconds = Percentile(orderedLatencies, 0.95);
            var maximumMilliseconds = orderedLatencies.Length == 0 ? (double?)null : orderedLatencies[^1];
            var measurementPassed = orderedLatencies.Length == GlobalHotkeySmokeSamples &&
                p95Milliseconds <= GlobalHotkeySmokeBudgetMilliseconds;

            var report = new
            {
                schemaVersion = 1,
                measuredAtUtc = DateTimeOffset.UtcNow,
                runtime = new
                {
                    framework = Environment.Version.ToString(),
                    operatingSystem = Environment.OSVersion.VersionString,
                    processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                },
                target = new
                {
                    expectedWindow = expectedTarget.ToInt64(),
                    processId = targetProcessId,
                    foregroundBeforeInjection = foregroundBeforeInjection.ToInt64(),
                    capturedWindow = capturedTarget.ToInt64(),
                    capturedFocusWindow = capturedFocus.ToInt64(),
                    caretAvailable,
                },
                hook = new
                {
                    installed = hookWasInstalled || capturedTarget != IntPtr.Zero,
                    syntheticInputEventsSent = totalSentInputCount,
                },
                picker = new
                {
                    window = pickerHandle.ToInt64(),
                    visible = pickerVisible,
                    foreground = pickerForeground,
                },
                categoryCache = new
                {
                    reusedOnUnchangedData = categoryCacheReused,
                    invalidatedBySearchResults = searchInvalidatedCategoryCache,
                    reusedAfterSearchRefresh = categoryCacheReusedAfterSearch,
                    passed = categoryCacheReused &&
                        searchInvalidatedCategoryCache &&
                        categoryCacheReusedAfterSearch,
                },
                measurement = new
                {
                    samples = orderedLatencies.Length,
                    medianMilliseconds,
                    p95Milliseconds,
                    maximumMilliseconds,
                    valuesMilliseconds = orderedLatencies,
                    budgetMilliseconds = GlobalHotkeySmokeBudgetMilliseconds,
                    passed = measurementPassed,
                    boundaries = new
                    {
                        hookDispatchMilliseconds = Summary(hookDispatchLatencies),
                        targetCaptureMilliseconds = Summary(targetCaptureLatencies),
                        showPickerMilliseconds = Summary(showPickerLatencies),
                        showToRenderMilliseconds = Summary(showToRenderLatencies),
                        showPickerStages = new
                        {
                            resetMilliseconds = Summary(resetLatencies),
                            categoryMilliseconds = Summary(categoryLatencies),
                            positionMilliseconds = Summary(positionLatencies),
                            showMilliseconds = Summary(showLatencies),
                            activateMilliseconds = Summary(activateLatencies),
                            focusMilliseconds = Summary(focusLatencies),
                        },
                    },
                },
                passed = passed && measurementPassed,
                error,
                limitations = new[]
                {
                    "Win + . is generated with SendInput rather than a physical keyboard.",
                    "The smoke proves hook, foreground capture and visible-window activation; it does not select or insert an emoji.",
                },
            };

            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(
                    reportPath,
                    JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            }
            finally
            {
                pickerWindow.PrepareForProcessExit();
                pickerWindow.Close();
                ThemeManager.Shutdown();
                Shutdown(passed && measurementPassed ? 0 : 1);
            }
        }

        hotkey.HotkeyPressed += capturedTarget =>
        {
            try
            {
                var handlerStartedAt = Stopwatch.GetTimestamp();
                var hookMatchedAt = hotkey.LastMatchTimestamp;
                var targetCaptureStartedAt = Stopwatch.GetTimestamp();
                var capturedFocus = TextInjector.GetFocusedControl(capturedTarget);
                var capturedAccessibilityFocus = capturedFocus == capturedTarget
                    ? AccessibilityFocusSnapshot.Capture(capturedTarget)
                    : null;
                var caretAvailable = TextInjector.TryGetCaretRect(capturedTarget, out var caretRect);
                var targetCaptureEndedAt = Stopwatch.GetTimestamp();
                PreviousForegroundWindow = capturedTarget;
                PreviousFocusWindow = capturedFocus;
                PreviousAccessibilityFocus = capturedAccessibilityFocus;
                PreviousCaretRect = caretAvailable ? caretRect : null;

                var showPickerStartedAt = Stopwatch.GetTimestamp();
                pickerWindow.ShowPicker();
                var showPickerEndedAt = Stopwatch.GetTimestamp();
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var visibleAt = Stopwatch.GetTimestamp();
                    var pickerHandle = new WindowInteropHelper(pickerWindow).Handle;
                    var pickerVisible = pickerWindow.IsVisible && pickerHandle != IntPtr.Zero;
                    var pickerForeground = NativeMethods.GetForegroundWindow() == pickerHandle;
                    var latency = injectedAt == 0
                        ? (double?)null
                        : Stopwatch.GetElapsedTime(injectedAt, visibleAt).TotalMilliseconds;
                    var pathPassed = capturedTarget == expectedTarget &&
                        capturedFocus != IntPtr.Zero &&
                        foregroundBeforeInjection == expectedTarget &&
                        sentInputCount == 4 &&
                        pickerVisible &&
                        pickerForeground &&
                        latency.HasValue;

                    if (!pathPassed)
                    {
                        Finish(
                            false,
                            "One or more global-hotkey path assertions failed.",
                            capturedTarget,
                            capturedFocus,
                            caretAvailable,
                            pickerHandle,
                            pickerVisible,
                            pickerForeground);
                        return;
                    }

                    latencies.Add(latency!.Value);
                    hookDispatchLatencies.Add(ElapsedMilliseconds(hookMatchedAt, handlerStartedAt));
                    targetCaptureLatencies.Add(ElapsedMilliseconds(targetCaptureStartedAt, targetCaptureEndedAt));
                    showPickerLatencies.Add(ElapsedMilliseconds(showPickerStartedAt, showPickerEndedAt));
                    showToRenderLatencies.Add(ElapsedMilliseconds(showPickerEndedAt, visibleAt));
                    if (pickerWindow.LastShowPickerTiming is { } timing)
                    {
                        resetLatencies.Add(timing.ResetMilliseconds);
                        categoryLatencies.Add(timing.CategoryMilliseconds);
                        positionLatencies.Add(timing.PositionMilliseconds);
                        showLatencies.Add(timing.ShowMilliseconds);
                        activateLatencies.Add(timing.ActivateMilliseconds);
                        focusLatencies.Add(timing.FocusMilliseconds);
                    }
                    if (latencies.Count < GlobalHotkeySmokeSamples)
                    {
                        pickerWindow.DismissPicker();
                        injectionTimer.Interval = TimeSpan.FromMilliseconds(150);
                        injectionTimer.Start();
                        return;
                    }

                    var p95 = Percentile(latencies.OrderBy(value => value).ToArray(), 0.95);
                    var initialCategorySource = pickerWindow.EmojiItemsSourceForSmoke;
                    pickerWindow.LoadDefaultCategoryForSmoke();
                    categoryCacheReused = ReferenceEquals(initialCategorySource, pickerWindow.EmojiItemsSourceForSmoke);

                    pickerWindow.DisplaySearchForSmoke("heart");
                    var searchSource = pickerWindow.EmojiItemsSourceForSmoke;
                    pickerWindow.LoadDefaultCategoryForSmoke();
                    var refreshedCategorySource = pickerWindow.EmojiItemsSourceForSmoke;
                    searchInvalidatedCategoryCache = !ReferenceEquals(searchSource, refreshedCategorySource);
                    pickerWindow.LoadDefaultCategoryForSmoke();
                    categoryCacheReusedAfterSearch = ReferenceEquals(
                        refreshedCategorySource,
                        pickerWindow.EmojiItemsSourceForSmoke);

                    var cachePassed = categoryCacheReused &&
                        searchInvalidatedCategoryCache &&
                        categoryCacheReusedAfterSearch;
                    var passed = p95 <= GlobalHotkeySmokeBudgetMilliseconds && cachePassed;
                    Finish(
                        passed,
                        passed ? null : "Global hotkey performance or category-cache assertions failed.",
                        capturedTarget,
                        capturedFocus,
                        caretAvailable,
                        pickerHandle,
                        pickerVisible,
                        pickerForeground);
                }), DispatcherPriority.Render);
            }
            catch (Exception exception)
            {
                Finish(false, $"Hotkey handler failed: {exception.GetType().Name}", capturedTarget);
            }
        };

        pickerWindow.PreWarm();
        hotkey.Start();
        if (!hotkey.IsActive)
        {
            Finish(false, $"Global hook installation failed with Win32 error {Marshal.GetLastWin32Error()}.");
            return;
        }

        injectionTimer.Tick += (_, _) =>
        {
            injectionTimer.Stop();
            if (!NativeMethods.IsWindow(expectedTarget))
            {
                Finish(false, "The controlled target window closed before injection.");
                return;
            }

            ForceForegroundForGlobalHotkeySmoke(expectedTarget);
            foregroundBeforeInjection = NativeMethods.GetForegroundWindow();
            if (foregroundBeforeInjection != expectedTarget)
            {
                Finish(false, "The controlled target could not become foreground.");
                return;
            }

            var inputs = new[]
            {
                KeyboardInput(0x5B, keyUp: false),
                KeyboardInput(HotkeyBinding.VkPeriod, keyUp: false),
                KeyboardInput(HotkeyBinding.VkPeriod, keyUp: true),
                KeyboardInput(0x5B, keyUp: true),
            };
            injectedAt = Stopwatch.GetTimestamp();
            sentInputCount = NativeMethods.SendInput(
                (uint)inputs.Length,
                inputs,
                Marshal.SizeOf<NativeMethods.INPUT>());
            totalSentInputCount += sentInputCount;
            if (sentInputCount != inputs.Length)
            {
                Finish(false, $"SendInput accepted {sentInputCount} of {inputs.Length} events.");
            }
        };
        timeoutTimer.Tick += (_, _) => Finish(false, "Timed out waiting for the global hotkey path.");
        injectionTimer.Start();
        timeoutTimer.Start();
    }

    private static double? Percentile(double[] orderedValues, double percentile)
    {
        if (orderedValues.Length == 0)
        {
            return null;
        }

        var rank = Math.Clamp(percentile, 0, 1) * (orderedValues.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        if (lower == upper)
        {
            return orderedValues[lower];
        }

        var fraction = rank - lower;
        return orderedValues[lower] + ((orderedValues[upper] - orderedValues[lower]) * fraction);
    }

    private static object Summary(List<double> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        return new
        {
            median = Percentile(ordered, 0.5),
            p95 = Percentile(ordered, 0.95),
            maximum = ordered.Length == 0 ? (double?)null : ordered[^1],
        };
    }

    private static double ElapsedMilliseconds(long startedAt, long endedAt) =>
        Stopwatch.GetElapsedTime(startedAt, endedAt).TotalMilliseconds;

    private static void ForceForegroundForGlobalHotkeySmoke(IntPtr target)
    {
        var foregroundThread = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _);
        var thisThread = NativeMethods.GetCurrentThreadId();
        if (foregroundThread != thisThread && foregroundThread != 0)
        {
            NativeMethods.AttachThreadInput(foregroundThread, thisThread, true);
            NativeMethods.SetForegroundWindow(target);
            NativeMethods.AttachThreadInput(foregroundThread, thisThread, false);
        }
        else
        {
            NativeMethods.SetForegroundWindow(target);
        }
    }

    private static NativeMethods.INPUT KeyboardInput(ushort virtualKey, bool keyUp) =>
        new()
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
