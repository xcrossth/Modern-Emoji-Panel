using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace EmojiPicker
{
    public partial class App : Application
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>
        /// The foreground window when the hotkey fired; selected emoji are inserted
        /// into it. Set by the hotkey hook before the picker is shown.
        /// </summary>
        public static IntPtr PreviousForegroundWindow { get; set; }

        /// <summary>
        /// The child control that had keyboard focus when the hotkey fired (e.g.
        /// Explorer's Search or address edit); focus is restored to it on insert.
        /// </summary>
        public static IntPtr PreviousFocusWindow { get; set; }

        /// <summary>
        /// Screen rectangle of the target app's text caret at hotkey time, when it
        /// exposed one; the picker anchors to it (like the Windows 10 panel) instead
        /// of the mouse pointer. Null when unknown - the picker falls back to the mouse.
        /// </summary>
        public static System.Drawing.Rectangle? PreviousCaretRect { get; set; }

        // Run-again signals arriving this soon after startup are ignored: when
        // both the HKLM (all-users installer) and HKCU (tray toggle) Run values
        // exist, the second logon start would otherwise pop the picker open
        private static readonly TimeSpan StartupShowGrace = TimeSpan.FromSeconds(3);

        private readonly ClassicConflictDetector classicConflictDetector = new ClassicConflictDetector();
        private SingleInstanceCoordinator? singleInstance;
        private MainWindow? picker;
        private HotkeyListener? hotkey;
        private NotifyIcon? trayIcon;
        private ToolStripMenuItem? classicConflictItem;
        private System.Windows.Threading.DispatcherTimer? hookRearmTimer;
        private DateTime showGraceAnchorUtc;
        private bool graceActive;
        private bool? classicConflictActive;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var searchPreviewSmokeIndex = Array.FindIndex(
                e.Args,
                argument => string.Equals(argument, "--search-preview-smoke", StringComparison.Ordinal));
            if (searchPreviewSmokeIndex >= 0)
            {
                var reportPath = searchPreviewSmokeIndex + 1 < e.Args.Length
                    ? e.Args[searchPreviewSmokeIndex + 1]
                    : null;
                if (string.IsNullOrWhiteSpace(reportPath))
                {
                    Shutdown(2);
                }
                else
                {
                    RunSearchPreviewSmoke(reportPath);
                }

                return;
            }

            // A deterministic smoke path lets the monorepo verify that the WPF
            // shell, generated Emoji Baseline and Noto artwork initialise on the current OS
            // without taking the user's global hook, tray or Classic mutex. It
            // never reads or writes user settings and exits after the dispatcher
            // has completed the off-screen prewarm pass.
            if (e.Args.Contains("--foundation-smoke", StringComparer.Ordinal))
            {
                RunFoundationSmoke();
                return;
            }

            var identitySmokeIndex = Array.FindIndex(
                e.Args,
                argument => string.Equals(argument, "--product-identity-smoke", StringComparison.Ordinal));
            if (identitySmokeIndex >= 0)
            {
                var reportPath = identitySmokeIndex + 1 < e.Args.Length
                    ? e.Args[identitySmokeIndex + 1]
                    : null;
                Shutdown(string.IsNullOrWhiteSpace(reportPath)
                    ? 2
                    : ProductIdentitySmoke.Run(reportPath));
                return;
            }

            var insertionSmokeIndex = Array.FindIndex(
                e.Args,
                argument => string.Equals(argument, "--insertion-policy-smoke", StringComparison.Ordinal));
            if (insertionSmokeIndex >= 0)
            {
                var reportPath = insertionSmokeIndex + 1 < e.Args.Length
                    ? e.Args[insertionSmokeIndex + 1]
                    : null;
                Shutdown(string.IsNullOrWhiteSpace(reportPath)
                    ? 2
                    : InsertionPolicySmoke.Run(reportPath));
                return;
            }

            var pickerSessionSmokeIndex = Array.FindIndex(
                e.Args,
                argument => string.Equals(argument, "--picker-session-smoke", StringComparison.Ordinal));
            if (pickerSessionSmokeIndex >= 0)
            {
                var reportPath = pickerSessionSmokeIndex + 1 < e.Args.Length
                    ? e.Args[pickerSessionSmokeIndex + 1]
                    : null;
                Shutdown(string.IsNullOrWhiteSpace(reportPath)
                    ? 2
                    : PickerSessionSmoke.Run(reportPath));
                return;
            }

            // Only one resident instance may own the global hook and tray icon
            if (!SingleInstanceCoordinator.TryAcquire(
                    ProductIdentity.MutexName,
                    ProductIdentity.ShowEventName,
                    out singleInstance))
            {
                Shutdown();
                return;
            }

            Logger.Initialize();
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Logger.Log($"=== Startup v{version} ===");
            Settings.Load();

            // The 3s show-grace only needs to suppress the one duplicate logon start
            // that happens when both the all-users (HKLM) and per-user (HKCU)
            // autostart entries exist. Without that duplicate, a run-again signal is
            // always a genuine user relaunch and should open the picker immediately.
            graceActive = IsStartupEnabled() && IsMachineStartupEnabled();

            // A resident utility should survive a bad frame: log the exception
            // and keep running rather than take Win+. down until relaunch
            DispatcherUnhandledException += (_, args) =>
            {
                Logger.LogAlways($"UNHANDLED (UI, continuing): {args.Exception}");
                args.Handled = true;
                trayIcon?.ShowBalloonTip(4000, ProductIdentity.ProductName,
                    $"Something went wrong; details in {Logger.LogPath}", ToolTipIcon.Warning);
            };
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                Logger.LogAlways($"FATAL: {args.ExceptionObject}");

            // Stay alive with no visible window until the hotkey shows the picker
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            ThemeManager.Initialize();

            picker = new MainWindow();
            picker.PreWarm(); // warm the render path so the first hotkey open is fast

            CreateTrayIcon();
            RefreshHotkeyOwnership(showConflictNotification: true);

            // Windows can silently drop a low-level hook (callback timeout, secure
            // desktop, session switch). Re-arm on session change and on a periodic
            // backstop so Win+. recovers without a restart.
            Microsoft.Win32.SystemEvents.SessionSwitch += OnSessionSwitch;
            hookRearmTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(60),
            };
            hookRearmTimer.Tick += (_, _) => RefreshHotkeyOwnership(showConflictNotification: false);
            hookRearmTimer.Start();

            // Now that the picker exists, start handling run-again signals (a signal
            // latched during startup is processed here). The event was created above.
            // Anchor the show-grace HERE, not at process start: a duplicate-logon
            // signal latched during a slow warm-up must still fall inside the grace
            // window when the loop finally consumes it, or the picker would pop open.
            showGraceAnchorUtc = DateTime.UtcNow;
            singleInstance!.ShowRequested += OnShowRequested;
            singleInstance.StartListening();
        }

        private void RunFoundationSmoke()
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            ThemeManager.Initialize();

            var smokeWindow = new MainWindow(loadUserActivity: false);
            smokeWindow.PreWarm();
            Dispatcher.BeginInvoke(
                new Action(async () =>
                {
                    if (!smokeWindow.IsLoaded ||
                        smokeWindow.CategoryTabs.Items.Count != 10 ||
                        smokeWindow.SkinTonePicker.Items.Count != 6 ||
                        smokeWindow.BaselineEntryCount != 3944 ||
                        !smokeWindow.BundledAssetsAvailable ||
                        smokeWindow.EmojiGrid.Items.Count == 0)
                    {
                        FinishFoundationSmoke(smokeWindow, exitCode: 1);
                        return;
                    }

                    var firstEmoji = smokeWindow.EmojiGrid.Items[0] as Emoji;
                    var decoded = firstEmoji == null
                        ? null
                        : await NotoEmojiAssetProvider.Shared.LoadAsync(firstEmoji.AssetPath, 32);
                    var missing = await NotoEmojiAssetProvider.Shared.LoadAsync(
                        "vendor/noto-emoji/v2.051/png/128/definitely-missing.png", 32);
                    var dpiWidths = new[] { 1.0, 1.25, 1.5, 1.75, 2.0, 2.25, 2.5 }
                        .Select(scale => NotoEmojiImage.CalculateDecodePixelWidth(32, scale))
                        .SequenceEqual(new[] { 32, 40, 48, 56, 64, 72, 80 });
                    await Task.WhenAll(smokeWindow.SmokeEntries.Take(300)
                        .Select(emoji => NotoEmojiAssetProvider.Shared.LoadAsync(emoji.AssetPath, 32)));
                    var realizedContainers = smokeWindow.RealizedEmojiContainerCount;
                    if (decoded == null || !decoded.IsFrozen || missing != null || !dpiWidths ||
                        NotoEmojiAssetProvider.Shared.CachedImageCount > 256 ||
                        realizedContainers <= 0 ||
                        realizedContainers >= smokeWindow.EmojiGrid.Items.Count)
                    {
                        FinishFoundationSmoke(smokeWindow, exitCode: 1);
                        return;
                    }

                    var missingAssetRoot = Path.Combine(Path.GetTempPath(), $"modern-emoji-picker-missing-assets-{Guid.NewGuid():N}");
                    var missingAssetWindowPassed = false;
                    try
                    {
                        var baselineCopy = Path.Combine(
                            missingAssetRoot,
                            EmojiCatalog.BaselineRelativePath.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(baselineCopy)!);
                        File.Copy(EmojiCatalog.ResolveBundledPath(EmojiCatalog.BaselineRelativePath), baselineCopy);
                        var missingAssetCatalog = EmojiCatalog.Load(missingAssetRoot);
                        var missingAssetWindow = new MainWindow(loadUserActivity: false, missingAssetCatalog);
                        missingAssetWindowPassed = missingAssetWindow.BaselineEntryCount == 3944 &&
                            !missingAssetWindow.BundledAssetsAvailable &&
                            missingAssetWindow.RepairGuidanceVisible;
                        missingAssetWindow.PrepareForProcessExit();
                        missingAssetWindow.Close();
                    }
                    finally
                    {
                        if (Directory.Exists(missingAssetRoot))
                        {
                            Directory.Delete(missingAssetRoot, recursive: true);
                        }
                    }

                    if (!missingAssetWindowPassed)
                    {
                        FinishFoundationSmoke(smokeWindow, exitCode: 1);
                        return;
                    }

                    smokeWindow.ShowInsertionFailureForSmoke(firstEmoji!, "Target validation failed for smoke test.");
                    if (!smokeWindow.IsVisible || !smokeWindow.InsertionErrorVisible || !smokeWindow.ExplicitCopyAvailable)
                    {
                        FinishFoundationSmoke(smokeWindow, exitCode: 1);
                        return;
                    }

                    // A normal window close must only dismiss the resident picker.
                    // OnClosing cancels this Close, leaving the reusable shell loaded.
                    smokeWindow.Close();
                    if (!smokeWindow.IsLoaded || smokeWindow.IsVisible)
                    {
                        FinishFoundationSmoke(smokeWindow, exitCode: 1);
                        return;
                    }

                    smokeWindow.SearchBox.Text = "smile";
                    var searchVerificationTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(500),
                    };
                    searchVerificationTimer.Tick += (_, _) =>
                    {
                        searchVerificationTimer.Stop();
                        var searchPassed = smokeWindow.CategoryHeader.Text == "Search results" &&
                            smokeWindow.EmojiGrid.Items.Count > 0;
                        FinishFoundationSmoke(smokeWindow, searchPassed ? 0 : 1);
                    };
                    searchVerificationTimer.Start();
                }),
                DispatcherPriority.ContextIdle);
        }

        private void FinishFoundationSmoke(MainWindow smokeWindow, int exitCode)
        {
            smokeWindow.PrepareForProcessExit();
            smokeWindow.Close();
            ThemeManager.Shutdown();
            Shutdown(exitCode);
        }

        private void RunSearchPreviewSmoke(string reportPath)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            ThemeManager.Initialize();

            var catalog = EmojiCatalog.Load();
            var smokeWindow = new MainWindow(loadUserActivity: false, catalog);
            smokeWindow.PreWarm();
            Dispatcher.BeginInvoke(
                new Action(async () =>
                {
                    var synthetic = CreateSearchTierFixtures();
                    var syntheticMatches = new EmojiSearchIndex(synthetic).Search("  HEART  ");
                    var expectedIds = new[]
                    {
                        "exact-early",
                        "exact-late",
                        "prefix",
                        "keyword",
                        "substring",
                    };
                    var expectedTiers = new[]
                    {
                        EmojiMatchTier.ExactShortName,
                        EmojiMatchTier.ExactShortName,
                        EmojiMatchTier.ShortNameTermPrefix,
                        EmojiMatchTier.Keyword,
                        EmojiMatchTier.Substring,
                    };
                    var tierOrderingPassed = syntheticMatches.Select(match => match.Emoji.Id).SequenceEqual(expectedIds) &&
                        syntheticMatches.Select(match => match.Tier).SequenceEqual(expectedTiers);

                    var englishResults = smokeWindow.SearchForSmoke("grinning face");
                    var thaiResults = smokeWindow.SearchForSmoke("หน้ายิ้มยิงฟัน");
                    var englishKeywordResults = smokeWindow.SearchForSmoke("cheerful");
                    var thaiKeywordResults = smokeWindow.SearchForSmoke("ยิ้มกว้าง");

                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    for (var i = 0; i < 100; i++)
                    {
                        _ = smokeWindow.SearchForSmoke(i % 2 == 0 ? "face" : "หัวใจ");
                    }

                    stopwatch.Stop();

                    smokeWindow.EmojiGrid.UpdateLayout();
                    var selected = smokeWindow.EmojiGrid.SelectedItem as Emoji;
                    var selectedContainer = selected == null
                        ? null
                        : smokeWindow.EmojiGrid.ItemContainerGenerator.ContainerFromItem(selected) as System.Windows.Controls.ListBoxItem;
                    var accessibleNamePassed = selected != null && selectedContainer != null &&
                        string.Equals(
                            System.Windows.Automation.AutomationProperties.GetName(selectedContainer),
                            selected.Name,
                            StringComparison.Ordinal);
                    var focusBefore = System.Windows.Input.Keyboard.FocusedElement;
                    var previewOpened = smokeWindow.OpenSelectedPreviewForSmoke();
                    var focusAfter = System.Windows.Input.Keyboard.FocusedElement;
                    var previewImage = selected == null
                        ? null
                        : await NotoEmojiAssetProvider.Shared.LoadAsync(selected.PreviewAssetPath, 160);
                    var englishSecondaryExpected = selected != null &&
                        !string.Equals(selected.Name, selected.EnglishName, StringComparison.CurrentCultureIgnoreCase);
                    var previewDetailsPassed = selected != null && previewOpened &&
                        string.Equals(smokeWindow.PreviewLocalizedNameText, selected.Name, StringComparison.Ordinal) &&
                        string.Equals(smokeWindow.PreviewEnglishNameText, selected.EnglishName, StringComparison.Ordinal) &&
                        string.Equals(smokeWindow.PreviewVersionText, $"Emoji {selected.EmojiVersion}", StringComparison.Ordinal) &&
                        string.Equals(smokeWindow.PreviewAssetPath, selected.PreviewAssetPath, StringComparison.Ordinal) &&
                        (smokeWindow.PreviewEnglishName.Visibility == Visibility.Visible) == englishSecondaryExpected &&
                        previewImage != null && previewImage.IsFrozen &&
                        ReferenceEquals(focusBefore, focusAfter);
                    smokeWindow.ClosePreviewForSmoke();
                    var previewDismissed = !smokeWindow.IsPreviewOpen;

                    var report = new
                    {
                        catalogEntries = catalog.Entries.Count,
                        tierOrderingPassed,
                        englishNamePassed = englishResults.FirstOrDefault().Emoji?.Id == "emoji-1f600" &&
                            englishResults.FirstOrDefault().Tier == EmojiMatchTier.ExactShortName,
                        thaiNamePassed = thaiResults.FirstOrDefault().Emoji?.Id == "emoji-1f600" &&
                            thaiResults.FirstOrDefault().Tier == EmojiMatchTier.ExactShortName,
                        englishKeywordPassed = englishKeywordResults.Any(match => match.Emoji.Id == "emoji-1f600" &&
                            match.Tier == EmojiMatchTier.Keyword),
                        thaiKeywordPassed = thaiKeywordResults.Any(match => match.Emoji.Id == "emoji-1f600" &&
                            match.Tier == EmojiMatchTier.Keyword),
                        searchIterations = 100,
                        searchElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                        hoverDelayMilliseconds = global::EmojiPicker.MainWindow.HoverPreviewDelay.TotalMilliseconds,
                        accessibleNamePassed,
                        previewDetailsPassed,
                        previewDismissed,
                        previewUses512Role = selected?.PreviewAssetPath.Contains("/png/512/", StringComparison.Ordinal) == true,
                        previewDecodedPixelWidth = (previewImage as System.Windows.Media.Imaging.BitmapSource)?.PixelWidth ?? 0,
                    };

                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
                        File.WriteAllText(
                            reportPath,
                            System.Text.Json.JsonSerializer.Serialize(
                                report,
                                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                        var passed = report.catalogEntries == 3944 &&
                            report.tierOrderingPassed &&
                            report.englishNamePassed &&
                            report.thaiNamePassed &&
                            report.englishKeywordPassed &&
                            report.thaiKeywordPassed &&
                            report.hoverDelayMilliseconds == 400 &&
                            report.accessibleNamePassed &&
                            report.previewDetailsPassed &&
                            report.previewDismissed &&
                            report.previewUses512Role &&
                            report.previewDecodedPixelWidth == 160;
                        FinishSearchPreviewSmoke(smokeWindow, passed ? 0 : 1);
                    }
                    catch
                    {
                        FinishSearchPreviewSmoke(smokeWindow, 1);
                    }
                }),
                DispatcherPriority.ContextIdle);
        }

        private static IReadOnlyList<Emoji> CreateSearchTierFixtures()
        {
            return
            [
                CreateSearchFixture("exact-late", "heart", 30),
                CreateSearchFixture("prefix", "red heart shape", 2),
                CreateSearchFixture("keyword", "love symbol", 1, englishKeywords: ["heart"]),
                CreateSearchFixture("substring", "sweetheart", 0),
                CreateSearchFixture("exact-early", "heart", 5),
            ];
        }

        private static Emoji CreateSearchFixture(
            string id,
            string englishName,
            int order,
            IReadOnlyList<string>? englishKeywords = null)
        {
            return new Emoji(
                id,
                "x",
                englishName,
                englishName,
                "ทดสอบ",
                "Symbols",
                id,
                englishKeywords ?? [],
                [],
                "17.0",
                "grid.png",
                "preview.png",
                order,
                99);
        }

        private void FinishSearchPreviewSmoke(MainWindow smokeWindow, int exitCode)
        {
            smokeWindow.PrepareForProcessExit();
            smokeWindow.Close();
            ThemeManager.Shutdown();
            Shutdown(exitCode);
        }

        private void OnHotkeyPressed(IntPtr targetWindow)
        {
            if (picker?.IsPickerSessionOpen == true)
            {
                Logger.Log("Hotkey while open -> ignored");
                return;
            }

            // Resolve the focused control and caret here (UI thread), off the hook
            // thread. The target app still has focus (our window isn't shown yet),
            // so this is the same state the hook would have captured - but a hung
            // target can no longer stall the low-level hook and get it removed.
            var focusWindow = TextInjector.GetFocusedControl(targetWindow);
            System.Drawing.Rectangle? caretRect =
                TextInjector.TryGetCaretRect(targetWindow, out var rect) ? rect : null;

            Logger.Log($"Hotkey pressed; target={targetWindow} focus={focusWindow} caret={(caretRect.HasValue ? caretRect.Value.ToString() : "none")}");

            PreviousForegroundWindow = targetWindow;
            PreviousFocusWindow = focusWindow;
            PreviousCaretRect = caretRect;
            picker?.ShowPicker();
        }

        private void OnSessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
        {
            // SystemEvents raises this on a background thread; re-arm on the UI
            // thread, which owns the hook's message loop
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshHotkeyOwnership(showConflictNotification: false);
                Logger.Log($"Session switch ({e.Reason}) -> hotkey ownership refreshed");
            }));
        }

        private void OnShowRequested()
        {
            // Snapshot the foreground state on the signal thread, at signal time.
            var target = NativeMethods.GetForegroundWindow();
            var focus = TextInjector.GetFocusedControl(target);
            System.Drawing.Rectangle? caret =
                TextInjector.TryGetCaretRect(target, out var caretRect) ? caretRect : null;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (picker?.IsPickerSessionOpen == true)
                {
                    Logger.Log("Show requested while open -> ignored");
                    return;
                }

                // Ignore signals right after startup ONLY when a duplicate logon
                // start is actually possible (both the HKLM all-users and HKCU
                // per-user Run values present); that duplicate would otherwise
                // pop the picker. Otherwise a run-again is a real user relaunch.
                if (graceActive && DateTime.UtcNow - showGraceAnchorUtc < StartupShowGrace)
                {
                    Logger.Log("Show requested (run-again) ignored during startup grace");
                    return;
                }

                Logger.Log("Show requested (run-again)");

                // Don't let the picker become its own insertion target when
                // it is already open; keep whatever target it had.
                if (picker == null || target != new System.Windows.Interop.WindowInteropHelper(picker).Handle)
                {
                    PreviousForegroundWindow = target;
                    PreviousFocusWindow = focus;
                    PreviousCaretRect = caret;
                }

                picker?.ShowPicker();
            }));
        }

        private void CreateTrayIcon()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open Modern Emoji Picker", null, (_, _) => ShowPickerFromTray());
            menu.Items.Add(new ToolStripSeparator());

            classicConflictItem = new ToolStripMenuItem("Classic Conflict: Win + . is disabled")
            {
                Enabled = false,
                Visible = false,
            };
            menu.Items.Add(classicConflictItem);
            menu.Items.Add("Check for Classic again", null, (_, _) =>
                RefreshHotkeyOwnership(showConflictNotification: true));
            menu.Items.Add(new ToolStripSeparator());

            var startupItem = new ToolStripMenuItem("Start with Windows")
            {
                Checked = IsStartupEnabled(),
                CheckOnClick = true,
            };
            startupItem.CheckedChanged += (_, _) => SetStartupEnabled(startupItem.Checked);
            if (IsMachineStartupEnabled())
            {
                // An all-users install manages autostart via HKLM, which this
                // per-user toggle can't change - show it as on and read-only
                startupItem.Checked = true;
                startupItem.CheckOnClick = false;
                startupItem.Enabled = false;
                startupItem.ToolTipText = "Enabled for all users by the installer";
            }

            menu.Items.Add(startupItem);

            var loggingItem = new ToolStripMenuItem("Debug logging")
            {
                Checked = Logger.Enabled,
                ToolTipText = Logger.LogPath,
            };
            loggingItem.Click += (_, _) =>
            {
                var on = Logger.Toggle();
                loggingItem.Checked = on;
                trayIcon?.ShowBalloonTip(
                    4000,
                    ProductIdentity.ProductName,
                    on ? $"Debug logging ON\n{Logger.LogPath}" : "Debug logging OFF",
                    ToolTipIcon.Info);
            };
            menu.Items.Add(loggingItem);

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit Modern Emoji Picker", null, (_, _) => ExitApplication());

            trayIcon = new NotifyIcon
            {
                // Ticket 14 supplies branded artwork. Until then use a neutral
                // system icon rather than reusing Classic's shipped icon.
                Icon = System.Drawing.SystemIcons.Application,
                Text = ProductIdentity.ProductName,
                Visible = true,
                ContextMenuStrip = menu,
            };
            trayIcon.DoubleClick += (_, _) => ShowPickerFromTray();
        }

        private void ShowPickerFromTray()
        {
            // Opened by mouse from the tray: there is no caret to anchor to, and
            // the target/caret left over from an earlier hotkey press would send
            // the emoji to a window the user isn't looking at any more. With no
            // target, a pick falls back to the clipboard - predictable.
            PreviousForegroundWindow = IntPtr.Zero;
            PreviousFocusWindow = IntPtr.Zero;
            PreviousCaretRect = null;
            picker?.ShowPicker();
        }

        private void RefreshHotkeyOwnership(bool showConflictNotification)
        {
            var conflict = classicConflictDetector.IsClassicRunning();
            var conflictChanged = classicConflictActive != conflict;
            classicConflictActive = conflict;

            if (classicConflictItem != null)
            {
                classicConflictItem.Visible = conflict;
            }

            if (conflict)
            {
                if (hotkey != null)
                {
                    hotkey.Dispose();
                    hotkey = null;
                    Logger.Log("Classic conflict detected -> Modern hotkey hook removed");
                }

                if ((showConflictNotification || conflictChanged) && trayIcon != null)
                {
                    trayIcon.ShowBalloonTip(
                        10000,
                        "Classic Emoji Picker is running",
                        "Modern did not take Win + . and did not stop Classic. Choose Exit from Classic's tray menu, then choose 'Check for Classic again' here.",
                        ToolTipIcon.Warning);
                }

                return;
            }

            if (hotkey == null)
            {
                hotkey = new HotkeyListener();
                hotkey.HotkeyPressed += OnHotkeyPressed;
                hotkey.Start();
                Logger.Log("Modern keyboard hook installed (Classic not detected)");
            }
            else
            {
                hotkey.Rearm();
            }

            if (showConflictNotification && conflictChanged && trayIcon != null)
            {
                trayIcon.ShowBalloonTip(
                    4000,
                    ProductIdentity.ProductName,
                    "Classic is no longer running. Modern now owns Win + .",
                    ToolTipIcon.Info);
            }
        }

        private void ExitApplication()
        {
            picker?.PrepareForProcessExit();
            Shutdown();
        }

        private static bool IsStartupEnabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath);
                return key?.GetValue(ProductIdentity.RunValueName) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// True when an all-users install registered autostart under HKLM
        /// (read-only from this per-user process; the tray toggle can't change it).
        /// </summary>
        private static bool IsMachineStartupEnabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(RunKeyPath);
                return key?.GetValue(ProductIdentity.RunValueName) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void SetStartupEnabled(bool enabled)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                if (key == null)
                {
                    return;
                }

                if (enabled)
                {
                    var exePath = Environment.ProcessPath;
                    if (exePath != null)
                    {
                        key.SetValue(ProductIdentity.RunValueName, $"\"{exePath}\"");
                    }
                }
                else
                {
                    key.DeleteValue(ProductIdentity.RunValueName, throwOnMissingValue: false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not update the startup setting: {ex.Message}",
                    ProductIdentity.ProductName, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            hookRearmTimer?.Stop();
            try
            {
                Microsoft.Win32.SystemEvents.SessionSwitch -= OnSessionSwitch;
            }
            catch (Exception)
            {
                // SystemEvents teardown is best-effort
            }

            hotkey?.Dispose();
            ThemeManager.Shutdown();
            singleInstance?.Dispose();

            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }

            base.OnExit(e);
        }
    }
}
