using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace EmojiPicker
{
    internal sealed record ShowPickerTiming(
        double ResetMilliseconds,
        double CategoryMilliseconds,
        double PositionMilliseconds,
        double ShowMilliseconds,
        double ActivateMilliseconds,
        double FocusMilliseconds,
        double TotalMilliseconds);

    internal sealed record VirtualizedScrollMeasurement(
        IReadOnlyList<double> FrameMilliseconds,
        IReadOnlyList<double> ScrollCommandMilliseconds,
        IReadOnlyList<double> RenderWaitMilliseconds);

    public partial class MainWindow : Window
    {
        private const int MaxPendingInsertions = 20;
        private const string RecentCategoryKey = "Recent";
        private const string SearchHeader = "Search results";

        // Emoji cell footprint in DIPs (40x40 border + 1px margin each side);
        // used to derive the grid's current column count for keyboard nav
        private const double ItemCellWidth = 42.0;

        // How long to wait after the last keystroke before filtering, so typing
        // stays smooth instead of re-rendering the grid on every character
        private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(120);
        internal static readonly TimeSpan HoverPreviewOpenDelay = TimeSpan.Zero;
        internal static readonly TimeSpan HoverPreviewCloseDelay = TimeSpan.FromMilliseconds(150);

        private const string DefaultCategoryKey = "Smileys & Emotion";

        private readonly DispatcherTimer searchTimer;
        private readonly DispatcherTimer previewCloseTimer;
        private readonly bool persistUserActivity;
        private readonly ActivityDataStore activityData;
        private System.Windows.Point? previewTargetScreenOrigin;
        private EmojiSearchIndex searchIndex;
        private List<Emoji> baselineEmojis = new List<Emoji>();
        private List<Emoji> allEmojis = new List<Emoji>();
        private List<Emoji> recentEmojis = new List<Emoji>();
        private List<EmojiCategory> categories = new List<EmojiCategory>();
        private string currentCategory = DefaultCategoryKey;
        private string? displayedCategoryKey;
        private int categoryDataVersion;
        private int displayedCategoryDataVersion = -1;
        private bool bundledAssetsAvailable;
        private bool isShowing;
        private bool allowProcessExit;
        private string? failedInsertionText;
        private PreviewOrigin previewOrigin;
        private EmojiVariantCatalog? variantCatalog;
        private SkinTonePreference currentSkinTone = SkinTonePreference.Neutral;
        private bool skinTonePickerReady;
        private bool variantMenuOpen;
        private readonly PickerSessionState sessionState = new();
        private readonly InsertionQueue<InsertionWorkItem> insertionQueue = new(MaxPendingInsertions);
        private DispatcherOperation? insertionPumpOperation;
        private bool insertionPumpRunning;
        private bool insertionInProgress;
        private bool pointerActivationSuppressed;
        private PickerViewSnapshot? lastInsertionSnapshot;
        private Action? processExitAfterQueue;

        internal ShowPickerTiming? LastShowPickerTiming { get; private set; }

        public MainWindow()
            : this(loadUserActivity: true)
        {
        }

        internal MainWindow(bool loadUserActivity)
            : this(loadUserActivity, catalogOverride: null)
        {
        }

        internal MainWindow(bool loadUserActivity, EmojiCatalogLoadResult? catalogOverride)
        {
            InitializeComponent();
            persistUserActivity = loadUserActivity;
            Width = Math.Clamp(Settings.Current.PickerWidth, MinWidth, 900);
            Height = Math.Clamp(Settings.Current.PickerHeight, MinHeight, 900);
            InitializeEmojis(catalogOverride);
            activityData = loadUserActivity
                ? new ActivityDataStore(
                    ProductIdentity.DataDirectory,
                    resolvedIdForSequence: sequence => variantCatalog?.ResolvedIdForSequence(sequence))
                : ActivityDataStore.CreateTransient();
            var activityPrune = activityData.PruneToBaseline(
                allEmojis.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal),
                variantCatalog?.ResolvedEntryIds ?? new HashSet<string>(StringComparer.Ordinal));
            if (activityPrune.Changed)
            {
                Logger.Log($"Pruned Activity Data after baseline load: recent={activityPrune.RecentRemoved}; ranking={activityPrune.RankingRemoved}");
            }

            searchIndex = new EmojiSearchIndex(allEmojis, activityData.GetLearnedScores);
            InitializeSkinTonePicker();
            RefreshRecentEmojis();
            ShowActivityRecoveryNotice();
            ApplyUiLanguage();

            searchTimer = new DispatcherTimer { Interval = SearchDebounce };
            searchTimer.Tick += (_, _) => RunSearch();
            previewCloseTimer = new DispatcherTimer { Interval = HoverPreviewCloseDelay };
            previewCloseTimer.Tick += (_, _) => HidePreview();

            CategoryTabs.ItemsSource = categories;
            CategoryTabs.SelectedIndex = categories.FindIndex(category => category.Key == currentCategory);
        }

        internal int BaselineEntryCount => baselineEmojis.Count;
        internal bool BundledAssetsAvailable => bundledAssetsAvailable;
        internal bool RepairGuidanceVisible => AssetRepairPanel.Visibility == Visibility.Visible;
        internal IReadOnlyList<Emoji> SmokeEntries => allEmojis;
        internal int RealizedEmojiContainerCount => Enumerable.Range(0, EmojiGrid.Items.Count)
            .Count(index => EmojiGrid.ItemContainerGenerator.ContainerFromIndex(index) != null);
        internal bool InsertionErrorVisible => InsertionErrorPanel.Visibility == Visibility.Visible;
        internal string InsertionErrorTextForSmoke => InsertionErrorText.Text;
        internal bool ExplicitCopyAvailable => ExplicitCopyButton.IsEnabled && ExplicitCopyButton.Visibility == Visibility.Visible;
        internal void ShowInsertionFailureForSmoke(Emoji emoji, string message) => ShowInsertionError(emoji, message);
        internal bool IsPreviewOpen => EmojiPreviewPopup.IsOpen;
        internal string PreviewLocalizedNameText => PreviewLocalizedName.Text;
        internal string PreviewEnglishNameText => PreviewEnglishName.Text;
        internal string PreviewVersionText => PreviewEmojiVersion.Text;
        internal string PreviewAssetPath => PreviewArtwork.AssetPath;
        internal System.Windows.Point? PreviewScreenOriginForSmoke =>
            EmojiPreviewPopup.IsOpen && EmojiPreviewPopup.Child is UIElement child
                ? child.PointToScreen(new System.Windows.Point(0, 0))
                : null;
        internal PickerInputMode InputMode => sessionState.Mode;
        internal bool IsPickerSessionOpen => IsVisible;
        internal string AccessibilityStatus => AutomationStatusText.Text;
        internal int PendingInsertionCount => insertionQueue.PendingCount;
        internal bool InsertionQueueFull => insertionQueue.IsFull;
        internal bool EmojiGridInteractiveForSmoke => EmojiGrid.IsHitTestVisible;
        internal bool PointerActivationSuppressedForSmoke => pointerActivationSuppressed;
        internal bool InsertionIdleForSmoke => !insertionPumpRunning && !insertionQueue.HasWork;
        internal void CommitEmojiForSmoke(Emoji emoji) => CommitEmoji(emoji, CommitGesture.Pointer);
        internal object? EmojiItemsSourceForSmoke => EmojiGrid.ItemsSource;
        internal void LoadDefaultCategoryForSmoke() => LoadCategory(DefaultCategoryKey);
        internal void DisplaySearchForSmoke(string query)
        {
            SearchBox.Text = query;
            RunSearch();
        }

        internal void FocusSearchForSmoke()
        {
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
        }

        internal async Task<IReadOnlyList<double>> MeasureWarmOpenToRenderProxyForSmokeAsync(int samples)
        {
            var results = new List<double>(samples);
            var originalShowActivated = ShowActivated;
            WindowStartupLocation = WindowStartupLocation.Manual;
            ShowActivated = false;
            Left = -32000;
            Top = -32000;

            try
            {
                for (var index = 0; index < samples; index++)
                {
                    SearchBox.Clear();
                    LoadCategory(DefaultCategoryKey);
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    Show();
                    await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render);
                    UpdateLayout();
                    stopwatch.Stop();
                    results.Add(stopwatch.Elapsed.TotalMilliseconds);
                    Hide();
                    await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle);
                }
            }
            finally
            {
                Hide();
                ShowActivated = originalShowActivated;
            }

            return results;
        }

        internal async Task<VirtualizedScrollMeasurement> MeasureVirtualizedScrollFramesForSmokeAsync(int samples)
        {
            var largestCategory = allEmojis
                .GroupBy(emoji => emoji.Category, StringComparer.Ordinal)
                .OrderByDescending(group => group.Count())
                .First();
            LoadCategory(largestCategory.Key);

            var originalShowActivated = ShowActivated;
            WindowStartupLocation = WindowStartupLocation.Manual;
            ShowActivated = false;
            Left = -32000;
            Top = -32000;
            Show();
            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render);
            UpdateLayout();

            try
            {
                var viewer = FindVisualChild<ScrollViewer>(EmojiGrid)
                    ?? throw new InvalidOperationException("The virtualized grid ScrollViewer was not created.");
                if (viewer.ScrollableHeight <= 0)
                {
                    throw new InvalidOperationException("The largest category did not produce a scrollable viewport.");
                }

                var results = new List<double>(samples);
                var scrollCommands = new List<double>(samples);
                var renderWaits = new List<double>(samples);
                for (var index = 0; index < samples; index++)
                {
                    var fraction = ((index * 37) % samples) / (double)Math.Max(1, samples - 1);
                    var frameStartedAt = System.Diagnostics.Stopwatch.GetTimestamp();
                    viewer.ScrollToVerticalOffset(viewer.ScrollableHeight * fraction);
                    var scrollEndedAt = System.Diagnostics.Stopwatch.GetTimestamp();
                    await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render);
                    var renderEndedAt = System.Diagnostics.Stopwatch.GetTimestamp();
                    results.Add(ElapsedMilliseconds(frameStartedAt, renderEndedAt));
                    scrollCommands.Add(ElapsedMilliseconds(frameStartedAt, scrollEndedAt));
                    renderWaits.Add(ElapsedMilliseconds(scrollEndedAt, renderEndedAt));

                    // Model a 60 Hz input cadence instead of enqueueing 100 synthetic
                    // jumps back-to-back. Do not drain the dispatcher to ContextIdle:
                    // real continuous scrolling starts the next input frame even when
                    // lower-priority image-completion work is still settling.
                    await Task.Delay(16);
                }

                return new VirtualizedScrollMeasurement(results, scrollCommands, renderWaits);
            }
            finally
            {
                Hide();
                ShowActivated = originalShowActivated;
            }
        }

        // The items panel hosting the emoji cells; cached after the first lookup.
        // Its ActualWidth is the viewport content width (scrollbar excluded),
        // which is stable regardless of scroll position or container recycling.
        private WpfToolkit.Controls.VirtualizingWrapPanel? emojiPanel;

        // Columns currently shown by the virtualizing wrap panel, derived from
        // the panel's width so Up/Down move exactly one visual row. Geometry of
        // realized containers is NOT used: recycled containers report garbage
        // positions once the grid has scrolled, which broke row navigation.
        private int ColumnsPerRow
        {
            get
            {
                if (EmojiGrid.Items.Count == 0)
                {
                    return 1;
                }

                emojiPanel ??= FindVisualChild<WpfToolkit.Controls.VirtualizingWrapPanel>(EmojiGrid);
                if (emojiPanel != null && emojiPanel.ActualWidth > 0)
                {
                    return Math.Max(1, (int)(emojiPanel.ActualWidth / ItemCellWidth));
                }

                // Before first layout: estimate from the ListBox width, allowing
                // for the 10px themed scrollbar (or the estimate lands one high)
                return Math.Max(1, (int)((EmojiGrid.ActualWidth - 12) / ItemCellWidth));
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    return match;
                }

                if (FindVisualChild<T>(child) is T nested)
                {
                    return nested;
                }
            }

            return null;
        }

        /// <summary>
        /// Renders the window once off-screen at startup so the WPF visual tree and
        /// Noto artwork path are JIT-warmed; the first real hotkey open is then
        /// as fast as subsequent ones instead of paying a cold-start cost.
        /// </summary>
        public void PreWarm()
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = -32000;
            Top = -32000;
            ShowActivated = false;
            Show();

            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    // Only hide if we're still the off-screen prewarm window. If a
                    // hotkey ShowPicker ran first (during startup), it repositioned
                    // us on-screen - don't yank it back closed.
                    if (Left <= -30000)
                    {
                        Hide();
                    }

                    ShowActivated = true;

                    // Startup allocates heavily (emoji database, glyph warm-up);
                    // return it before settling into the tray
                    Dispatcher.BeginInvoke(new Action(MemoryTrimmer.Trim), DispatcherPriority.ContextIdle);
                }),
                DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Brings the picker up ready to search, positioned near the cursor, and
        /// takes foreground. The window is reused across hotkey presses rather
        /// than recreated, so this resets it to a clean state each time.
        /// </summary>
        public void ShowPicker()
        {
            // A repeated hotkey while the Picker Session is open is a strict no-op.
            // In particular it must not reset query/category or leak through to the
            // Windows emoji panel underneath our hook.
            if (IsVisible)
            {
                Logger.Log("ShowPicker ignored because the Picker Session is already open");
                return;
            }

            // Ignore focus-loss triggered while we are bringing the window up
            var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            isShowing = true;
            sessionState.Begin();

            HidePreview();
            SearchBox.Clear();
            var resetEndedAt = System.Diagnostics.Stopwatch.GetTimestamp();

            // Open on Recent when there is history (like the Windows 10 picker),
            // otherwise the first content tab
            var openKey = recentEmojis.Count > 0 ? RecentCategoryKey : DefaultCategoryKey;
            var openIndex = categories.FindIndex(category => category.Key == openKey);
            if (CategoryTabs.SelectedIndex == openIndex)
            {
                LoadCategory(openKey); // no SelectionChanged will fire; refresh directly
            }
            else
            {
                CategoryTabs.SelectedIndex = openIndex; // fires SelectionChanged -> LoadCategory
            }
            var categoryEndedAt = System.Diagnostics.Stopwatch.GetTimestamp();

            PositionNearCursor();
            var positionEndedAt = System.Diagnostics.Stopwatch.GetTimestamp();

            Show();
            EnsureOnScreen();
            var showEndedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            Activate();
            var handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                ForceForeground(handle);
            }
            var activateEndedAt = System.Diagnostics.Stopwatch.GetTimestamp();

            FocusBrowseGrid();
            AnnounceStatus("Browse mode. Use arrow keys to choose an emoji.", busy: false);
            var focusEndedAt = System.Diagnostics.Stopwatch.GetTimestamp();

            LastShowPickerTiming = new ShowPickerTiming(
                ElapsedMilliseconds(startedAt, resetEndedAt),
                ElapsedMilliseconds(resetEndedAt, categoryEndedAt),
                ElapsedMilliseconds(categoryEndedAt, positionEndedAt),
                ElapsedMilliseconds(positionEndedAt, showEndedAt),
                ElapsedMilliseconds(showEndedAt, activateEndedAt),
                ElapsedMilliseconds(activateEndedAt, focusEndedAt),
                ElapsedMilliseconds(startedAt, focusEndedAt));

            Logger.Log($"ShowPicker done in {LastShowPickerTiming.TotalMilliseconds:F1}ms: Left={Left:F0} Top={Top:F0} " +
                $"W={Width} H={Height} foreground={NativeMethods.GetForegroundWindow()} thisHwnd={handle}");

            // Clear the guard once the show/activation storm has settled
            Dispatcher.BeginInvoke(new Action(() => isShowing = false), System.Windows.Threading.DispatcherPriority.Background);
        }

        private static double ElapsedMilliseconds(long startedAt, long endedAt) =>
            System.Diagnostics.Stopwatch.GetElapsedTime(startedAt, endedAt).TotalMilliseconds;

        /// <summary>
        /// Brings our window to the foreground even though the hotkey fired from
        /// another app. A background process can't normally steal focus, so we
        /// briefly attach to the current foreground thread's input queue.
        /// </summary>
        private static void ForceForeground(IntPtr hwnd)
        {
            var foregroundThread = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), out _);
            var thisThread = NativeMethods.GetCurrentThreadId();

            if (foregroundThread != thisThread && foregroundThread != 0)
            {
                NativeMethods.AttachThreadInput(foregroundThread, thisThread, true);
                NativeMethods.SetForegroundWindow(hwnd);
                NativeMethods.AttachThreadInput(foregroundThread, thisThread, false);
            }
            else
            {
                NativeMethods.SetForegroundWindow(hwnd);
            }
        }

        private void PositionNearCursor()
        {
            // Prefer the captured text caret. When a target does not expose one,
            // centre on that same target window and monitor (not the mouse, which
            // may already have moved to a different display).
            System.Drawing.Rectangle? targetRect = null;
            if (App.PreviousForegroundWindow != IntPtr.Zero &&
                NativeMethods.GetWindowRect(App.PreviousForegroundWindow, out var nativeTargetRect))
            {
                targetRect = System.Drawing.Rectangle.FromLTRB(
                    nativeTargetRect.Left,
                    nativeTargetRect.Top,
                    nativeTargetRect.Right,
                    nativeTargetRect.Bottom);
            }

            // The anchor and Screen.WorkingArea are in physical pixels, but WPF's
            // Left/Top are in device-independent units. Convert with the window's DPI
            // scale, or the panel lands off-screen on scaled/high-DPI displays.
            var hwnd = new WindowInteropHelper(this).EnsureHandle();
            var dpiWindow = App.PreviousForegroundWindow != IntPtr.Zero
                ? App.PreviousForegroundWindow
                : hwnd;
            double scale = NativeMethods.GetDpiForWindow(dpiWindow) / 96.0;
            if (scale <= 0)
            {
                scale = 1.0;
            }

            var screen = App.PreviousCaretRect is System.Drawing.Rectangle caret
                ? System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(caret.Left, caret.Bottom))
                : App.PreviousForegroundWindow != IntPtr.Zero
                    ? System.Windows.Forms.Screen.FromHandle(App.PreviousForegroundWindow)
                    : System.Windows.Forms.Screen.PrimaryScreen!;
            var area = screen.WorkingArea;
            var placement = PickerPlacement.Calculate(
                App.PreviousCaretRect,
                targetRect,
                area,
                (int)Math.Ceiling(Width * scale),
                (int)Math.Ceiling(Height * scale));

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = placement.Left / scale;
            Top = placement.Top / scale;

            Logger.Log($"PositionPicker: anchor={placement.Anchor} scale={scale} " +
                $"area=[{area.Left},{area.Top},{area.Right},{area.Bottom}] => DIP Left={Left:F0} Top={Top:F0}");
        }

        /// <summary>
        /// Last line of defence against the window opening off-screen (e.g. mixed-DPI
        /// monitors): if its bounds fall outside every display, recentre on the primary.
        /// </summary>
        private void EnsureOnScreen()
        {
            double vsLeft = SystemParameters.VirtualScreenLeft;
            double vsTop = SystemParameters.VirtualScreenTop;
            double vsRight = vsLeft + SystemParameters.VirtualScreenWidth;
            double vsBottom = vsTop + SystemParameters.VirtualScreenHeight;

            bool onScreen = Left < vsRight && Left + Width > vsLeft && Top < vsBottom && Top + Height > vsTop;
            if (!onScreen)
            {
                var work = SystemParameters.WorkArea; // primary monitor, in DIPs
                Left = work.Left + ((work.Width - Width) / 2);
                Top = work.Top + ((work.Height - Height) / 2);
                Logger.Log($"EnsureOnScreen: off-screen, recentred to Left={Left:F0} Top={Top:F0}");
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (sessionState.Mode == PickerInputMode.Browse)
            {
                FocusBrowseGrid();
            }
        }

        private void FocusBrowseGrid()
        {
            sessionState.EnterBrowse();
            EmojiGrid.Focus();
            Keyboard.Focus(EmojiGrid);
            if (EmojiGrid.SelectedItem != null)
            {
                EmojiGrid.ScrollIntoView(EmojiGrid.SelectedItem);
                EmojiGrid.UpdateLayout();
                FocusSelectedEmojiContainer();
            }
        }

        private void FocusSelectedEmojiContainer()
        {
            if (sessionState.Mode == PickerInputMode.Browse &&
                EmojiGrid.SelectedItem != null &&
                EmojiGrid.ItemContainerGenerator.ContainerFromItem(EmojiGrid.SelectedItem) is ListBoxItem item)
            {
                item.Focus();
                Keyboard.Focus(item);
            }
        }

        private void MainWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allow dragging the window when clicking anywhere on it
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        // Popularity tier assigned to emoji absent from the frequency data
        // (rarely used, or newer than the dataset and not in its supplement)
        private const int UnrankedPopularity = 99;

        private void InitializeEmojis(EmojiCatalogLoadResult? catalogOverride)
        {
            // The generated, pinned Emoji 17 baseline is the runtime source of
            // truth. No Windows emoji font or runtime network request is involved.
            var popularity = LoadResourceMap<int>("popularity.json");
            var catalog = catalogOverride ?? EmojiCatalog.Load();
            baselineEmojis = catalog.Entries.ToList();
            foreach (var emoji in baselineEmojis)
            {
                emoji.Popularity = popularity.TryGetValue(NormalizeEmoji(emoji.Character), out var tier)
                    ? tier
                    : UnrankedPopularity;
            }

            variantCatalog = baselineEmojis.Count > 0 ? new EmojiVariantCatalog(baselineEmojis) : null;
            currentSkinTone = Settings.Current.PreferredSkinTone;
            RebuildResolvedEntries();

            bundledAssetsAvailable = catalog.AssetSetAvailable;
            AssetRepairPanel.Visibility = catalog.AssetSetAvailable ? Visibility.Collapsed : Visibility.Visible;
            if (!string.IsNullOrWhiteSpace(catalog.ErrorMessage))
            {
                AssetRepairMessage.Text = $"{catalog.ErrorMessage} Repair or reinstall Modern Emoji Picker.";
            }

            categories = CreateCategories(allEmojis);
        }

        private void RebuildResolvedEntries()
        {
            allEmojis = variantCatalog == null
                ? new List<Emoji>()
                : variantCatalog.BaseEntries
                    .Select(entry => variantCatalog.Resolve(entry, currentSkinTone).ToPresentation())
                    .ToList();
            categoryDataVersion++;
        }

        private void InitializeSkinTonePicker()
        {
            var thai = Localizer.IsThai;
            var iconBase = baselineEmojis.FirstOrDefault(entry =>
                string.Equals(entry.CanonicalSequence, "1F9D2", StringComparison.OrdinalIgnoreCase));
            string Icon(SkinTonePreference preference) =>
                iconBase == null || variantCatalog == null
                    ? string.Empty
                    : variantCatalog.Resolve(iconBase, preference).ToPresentation().AssetPath;
            var options = new List<SkinToneOption>
            {
                new(SkinTonePreference.Neutral, thai ? "กลาง (สีเหลือง)" : "Neutral (yellow)", Icon(SkinTonePreference.Neutral)),
                new(SkinTonePreference.Light, thai ? "สีผิวอ่อน" : "Light skin tone", Icon(SkinTonePreference.Light)),
                new(SkinTonePreference.MediumLight, thai ? "สีผิวขาวเหลือง" : "Medium-light skin tone", Icon(SkinTonePreference.MediumLight)),
                new(SkinTonePreference.Medium, thai ? "สีผิวปานกลาง" : "Medium skin tone", Icon(SkinTonePreference.Medium)),
                new(SkinTonePreference.MediumDark, thai ? "สีผิวเข้มปานกลาง" : "Medium-dark skin tone", Icon(SkinTonePreference.MediumDark)),
                new(SkinTonePreference.Dark, thai ? "สีผิวเข้ม" : "Dark skin tone", Icon(SkinTonePreference.Dark)),
            };

            SkinTonePicker.ItemsSource = options;
            SkinTonePicker.SelectedItem = options.Single(option => option.Preference == currentSkinTone);
            skinTonePickerReady = true;
        }

        internal void ApplyRuntimeSettings()
        {
            currentSkinTone = Settings.Current.PreferredSkinTone;
            RebuildResolvedEntries();
            searchIndex = new EmojiSearchIndex(allEmojis, activityData.GetLearnedScores);
            categories = CreateCategories(allEmojis);
            CategoryTabs.ItemsSource = categories;
            skinTonePickerReady = false;
            InitializeSkinTonePicker();
            ApplyUiLanguage();
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                LoadCategory(currentCategory);
            }
            else
            {
                RunSearch();
            }
        }

        private void ApplyUiLanguage()
        {
            SearchWatermark.Text = Localizer.Text("Keep typing to find an emoji", "พิมพ์ต่อเพื่อค้นหา Emoji");
            AssetRepairTitle.Text = Localizer.Text("Emoji artwork is unavailable", "ไม่พบภาพ Emoji");
            AssetRepairMessage.Text = Localizer.Text(
                "Repair or reinstall Modern Emoji Picker to restore the bundled Noto artwork.",
                "Repair หรือติดตั้ง Modern Emoji Picker ใหม่เพื่อคืนภาพ Noto ที่มากับแอป");
            ActivityNoticeDismissButton.Content = Localizer.Text("Dismiss", "ปิดข้อความ");
            ExplicitCopyButton.Content = Localizer.Text("Copy", "คัดลอก");
            SkinTonePicker.ToolTip = Localizer.Text(
                "Default skin tone (Alt+T). Right-click or press Alt+Down for a one-shot mixed-tone Variant Override.",
                "สีผิวเริ่มต้น (Alt+T) คลิกขวาหรือกด Alt+Down เพื่อเลือก Variant Override แบบครั้งเดียว");
            CloseButton.ToolTip = Localizer.Text("Close picker", "ปิด Picker");
        }

        private void SkinTonePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!skinTonePickerReady || SkinTonePicker.SelectedItem is not SkinToneOption option ||
                option.Preference == currentSkinTone)
            {
                return;
            }

            var selectedBaseId = (EmojiGrid.SelectedItem as Emoji)?.Id;
            currentSkinTone = option.Preference;
            Settings.SetGlobalSkinTone(currentSkinTone);
            RebuildResolvedEntries();
            searchIndex = new EmojiSearchIndex(allEmojis, activityData.GetLearnedScores);

            var selectedCategoryIndex = Math.Max(0, categories.FindIndex(category => category.Key == currentCategory));
            categories = CreateCategories(allEmojis);
            CategoryTabs.ItemsSource = categories;
            CategoryTabs.SelectedIndex = selectedCategoryIndex;

            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                LoadCategory(currentCategory);
            }
            else
            {
                RunSearch();
            }

            if (!string.IsNullOrEmpty(selectedBaseId))
            {
                var selected = EmojiGrid.Items.Cast<Emoji>().FirstOrDefault(entry => entry.Id == selectedBaseId);
                if (selected != null)
                {
                    EmojiGrid.SelectedItem = selected;
                    EmojiGrid.ScrollIntoView(selected);
                }
            }

            Logger.Log($"Global skin tone changed to {currentSkinTone}");
        }

        private static List<EmojiCategory> CreateCategories(IReadOnlyList<Emoji> emojis)
        {
            var thai = Localizer.IsThai;
            return new List<EmojiCategory>
            {
                CreateCategory(RecentCategoryKey, "1F550", thai ? "ใช้ล่าสุด" : "Recent", emojis),
                CreateCategory("Smileys & Emotion", "1F600", thai ? "หน้ายิ้มและอารมณ์" : "Smileys & Emotion", emojis),
                CreateCategory("People & Body", "1F9D1", thai ? "ผู้คนและร่างกาย" : "People & Body", emojis),
                CreateCategory("Animals & Nature", "1F43B", thai ? "สัตว์และธรรมชาติ" : "Animals & Nature", emojis),
                CreateCategory("Food & Drink", "1F355", thai ? "อาหารและเครื่องดื่ม" : "Food & Drink", emojis),
                CreateCategory("Travel & Places", "1F697", thai ? "การเดินทางและสถานที่" : "Travel & Places", emojis),
                CreateCategory("Activities", "26BD", thai ? "กิจกรรม" : "Activities", emojis),
                CreateCategory("Objects", "1F4A1", thai ? "สิ่งของ" : "Objects", emojis),
                CreateCategory("Symbols", "2764 FE0F", thai ? "สัญลักษณ์" : "Symbols", emojis),
                CreateCategory("Flags", "1F1F9 1F1ED", thai ? "ธง" : "Flags", emojis),
            };
        }

        private static EmojiCategory CreateCategory(
            string key,
            string iconCanonicalSequence,
            string displayName,
            IReadOnlyList<Emoji> emojis)
        {
            var icon = emojis.FirstOrDefault(emoji =>
                    string.Equals(emoji.BaseCanonicalSequence, iconCanonicalSequence, StringComparison.OrdinalIgnoreCase)) ??
                emojis.FirstOrDefault(emoji => emoji.Category == key);
            return new EmojiCategory(key, icon?.AssetPath ?? string.Empty, displayName);
        }

        // The legacy popularity data can differ on the FE0F variation selector;
        // strip it so lookups line up.
        private static string NormalizeEmoji(string text) => text.Replace("\uFE0F", string.Empty);

        private static Dictionary<string, T> LoadResourceMap<T>(string fileName)
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/Resources/{fileName}");
                using var stream = Application.GetResourceStream(uri)?.Stream;
                if (stream != null)
                {
                    var map = JsonSerializer.Deserialize<Dictionary<string, T>>(stream);
                    if (map != null)
                    {
                        return map;
                    }
                }
            }
            catch (Exception)
            {
                // Search still works (on names, in category order) without the data
            }

            return new Dictionary<string, T>();
        }

        private void LoadCategory(string categoryKey)
        {
            if (EmojiGrid == null)
            {
                return; // UI not ready yet
            }

            CategoryHeader.Text = categories.FirstOrDefault(category => category.Key == categoryKey)?.DisplayName ?? categoryKey;
            if (string.Equals(displayedCategoryKey, categoryKey, StringComparison.Ordinal) &&
                displayedCategoryDataVersion == categoryDataVersion &&
                EmojiGrid.ItemsSource != null)
            {
                EmojiGrid.SelectedIndex = EmojiGrid.Items.Count > 0 ? 0 : -1;
                if (EmojiGrid.SelectedItem != null)
                {
                    EmojiGrid.ScrollIntoView(EmojiGrid.SelectedItem);
                }

                Logger.Log($"LoadCategory '{categoryKey}' reused {EmojiGrid.Items.Count} items");
                return;
            }

            List<Emoji> categoryEmojis = categoryKey == RecentCategoryKey
                ? recentEmojis.ToList()
                : allEmojis.Where(emoji => emoji.Category == categoryKey).ToList();

            Logger.Log($"LoadCategory '{categoryKey}' -> {categoryEmojis.Count} items");
            ShowEmojis(categoryEmojis, categoryKey);
        }

        private void ShowEmojis(List<Emoji> emojis, string? categoryKey = null)
        {
            HidePreview();
            EmojiGrid.ItemsSource = emojis;
            displayedCategoryKey = categoryKey;
            displayedCategoryDataVersion = categoryDataVersion;
            EmojiGrid.SelectedIndex = emojis.Count > 0 ? 0 : -1;
            if (EmojiGrid.SelectedItem != null)
            {
                EmojiGrid.ScrollIntoView(EmojiGrid.SelectedItem);
            }

            if (Logger.Enabled)
            {
                // Confirm virtualization: realized containers should stay ~visible-only
                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        var realized = 0;
                        for (var i = 0; i < EmojiGrid.Items.Count; i++)
                        {
                            if (EmojiGrid.ItemContainerGenerator.ContainerFromIndex(i) != null)
                            {
                                realized++;
                            }
                        }

                        Logger.Log($"Grid realized {realized}/{EmojiGrid.Items.Count} containers");
                    }),
                    DispatcherPriority.Loaded);
            }
        }

        private void CategoryTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryTabs.SelectedItem is EmojiCategory category)
            {
                Logger.Log($"TabSelectionChanged -> {category.Key}");
                currentCategory = category.Key;
                if (string.IsNullOrEmpty(SearchBox.Text))
                {
                    LoadCategory(category.Key);
                }
                else
                {
                    SearchBox.Clear(); // triggers TextChanged, which loads the category
                }

                FocusBrowseGrid();
            }
        }

        private void SearchBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            sessionState.EnterSearch();
            AnnounceStatus("Search mode. Type an English or Thai name.", busy: false);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (EmojiGrid == null)
            {
                return; // UI not ready yet
            }

            searchTimer.Stop();
            HidePreview();
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                // Clearing the box should restore the category instantly
                LoadCategory(currentCategory);
            }
            else
            {
                sessionState.EnterSearch();
                // Debounce: filter once typing pauses, not on every keystroke
                searchTimer.Start();
            }
        }

        private void RunSearch()
        {
            searchTimer.Stop();
            if (EmojiGrid == null || string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                return;
            }

            // Normalize in the immutable bilingual index so UI locale never
            // changes which English or Thai CLDR metadata can be discovered.
            var searchText = SearchBox.Text;
            if (searchText.Length == 0)
            {
                return;
            }

            var matches = searchIndex.Search(searchText);
            var filteredEmojis = matches
                .Select(match => match.Emoji)
                .ToList();

            Logger.Log($"Search query -> exact={matches.Count(match => match.Tier == EmojiMatchTier.ExactShortName)}, " +
                $"prefix={matches.Count(match => match.Tier == EmojiMatchTier.ShortNameTermPrefix)}, " +
                $"keyword={matches.Count(match => match.Tier == EmojiMatchTier.Keyword)}, " +
                $"substring={matches.Count(match => match.Tier == EmojiMatchTier.Substring)}");
            CategoryHeader.Text = SearchHeader;
            ShowEmojis(filteredEmojis);
            AnnounceStatus($"Search results: {filteredEmojis.Count} emoji.", busy: false);
        }

        internal IReadOnlyList<EmojiSearchMatch> SearchForSmoke(string query) => searchIndex.Search(query);

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // While an IME is composing (CJK etc.), Enter commits the candidate and
            // the arrows move the candidate window - let the IME have those keys
            // instead of hijacking them for grid nav / emoji commit.
            if (e.Key == Key.ImeProcessed || e.Key == Key.DeadCharProcessed)
            {
                return;
            }

            if (e.Key == Key.Escape && (EmojiPreviewPopup.IsOpen || previewCloseTimer.IsEnabled))
            {
                HidePreview();
                e.Handled = true;
                return;
            }

            // In Browse, every non-modifier key except Escape belongs to the app
            // the user was typing in. Capture the physical key before WPF translates
            // it with the Picker's per-app keyboard layout (which may differ from
            // the target's layout), then dismiss and replay it to the target.
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (sessionState.Mode == PickerInputMode.Browse && key != Key.Escape &&
                TypingHandoffInput.TryCaptureKeyStroke(
                    KeyInterop.VirtualKeyFromKey(key),
                    GetShortcutModifiers(),
                    out var keyStroke))
            {
                e.Handled = true;
                BeginTypingHandoff(keyStroke);
                return;
            }

            if (e.Key == Key.F1)
            {
                OpenKeyboardPreview();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                HidePreview();
                sessionState.EnterSearch();
                SearchBox.Focus();
                Keyboard.Focus(SearchBox);
                SearchBox.SelectAll();
                e.Handled = true;
                return;
            }

            // Alt combinations arrive as Key.System in WPF; use the underlying
            // key so Alt+T and Alt+Down remain keyboard-accessible.
            // Search navigation remains key-based. Browse keys have already been
            // handed back to the target above.
            switch (key)
            {
                case Key.Enter:
                    // Apply any pending debounced search so the selection is current
                    if (searchTimer.IsEnabled)
                    {
                        RunSearch();
                    }

                    CommitSelectedEmoji((Keyboard.Modifiers & ModifierKeys.Shift) != 0
                        ? CommitGesture.ShiftEnter
                        : CommitGesture.Enter);
                    e.Handled = true;
                    break;
                case Key.Tab:
                    // Cycle categories from the keyboard (the tab strip isn't
                    // reliably clickable when the picker isn't the active window)
                    SwitchCategory((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? -1 : 1);
                    e.Handled = true;
                    break;
                case Key.Left:
                case Key.Right:
                    // Plain arrows browse the emoji grid (focus stays in the
                    // search box). With Ctrl and/or Shift they are text editing
                    // - word jump and character/word selection - so leave them
                    // unhandled and let the search box do its normal thing.
                    if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0)
                    {
                        break;
                    }

                    MoveSelection(key == Key.Left ? -1 : 1);
                    e.Handled = true;
                    break;
                case Key.Up:
                    MoveSelection(-ColumnsPerRow);
                    e.Handled = true;
                    break;
                case Key.Down:
                    if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0 && !SkinTonePicker.IsKeyboardFocusWithin)
                    {
                        OpenSelectedVariantOverrideMenu();
                        e.Handled = true;
                        break;
                    }

                    MoveSelection(ColumnsPerRow);
                    e.Handled = true;
                    break;
                case Key.T:
                    if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
                    {
                        SkinTonePicker.Focus();
                        Keyboard.Focus(SkinTonePicker);
                        e.Handled = true;
                    }

                    break;
            }

        }

        private void MainWindow_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sessionState.Mode != PickerInputMode.Browse ||
                !TypingHandoffInput.TryCaptureCommittedText(e.Text, out var committedText))
            {
                return;
            }

            // Fallback for IME/dead-key commits that do not expose a replayable
            // physical key through PreviewKeyDown.
            e.Handled = true;
            BeginTypingHandoff(committedText);
        }

        private void SwitchCategory(int direction)
        {
            var count = categories.Count;
            if (count == 0)
            {
                return;
            }

            // Wrap around; SelectionChanged loads the category and refocuses search
            var next = (((CategoryTabs.SelectedIndex + direction) % count) + count) % count;
            CategoryTabs.SelectedIndex = next;
        }

        private void MoveSelection(int delta)
        {
            if (EmojiGrid.Items.Count == 0)
            {
                return;
            }

            HidePreview();
            var index = EmojiGrid.SelectedIndex < 0 ? 0 : EmojiGrid.SelectedIndex + delta;
            EmojiGrid.SelectedIndex = Math.Clamp(index, 0, EmojiGrid.Items.Count - 1);
            EmojiGrid.ScrollIntoView(EmojiGrid.SelectedItem);
            EmojiGrid.UpdateLayout();
            FocusSelectedEmojiContainer();
            Logger.Log($"MoveSelection delta={delta} (columns={ColumnsPerRow}) -> index {EmojiGrid.SelectedIndex}");
        }

        private void EmojiGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EmojiGrid.SelectedItem is Emoji emoji)
            {
                AnnounceStatus($"Selected {emoji.Name}.", busy: insertionQueue.HasWork);
            }
        }

        private void AnnounceStatus(string message, bool busy)
        {
            AutomationStatusText.Text = message;
            System.Windows.Automation.AutomationProperties.SetItemStatus(
                EmojiGrid,
                busy ? $"Busy. {message}" : message);
        }

        private void CommitSelectedEmoji(CommitGesture gesture)
        {
            if (EmojiGrid.SelectedItem is Emoji emoji)
            {
                CommitEmoji(emoji, gesture);
            }
        }

        private void EmojiItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem { DataContext: Emoji emoji })
            {
                e.Handled = true;
                CommitEmoji(emoji, CommitGesture.Pointer);
            }
        }

        private void EmojiItem_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem { DataContext: Emoji emoji } item && OpenVariantOverrideMenu(item, emoji))
            {
                e.Handled = true;
            }
        }

        private bool OpenVariantOverrideMenu(ListBoxItem item, Emoji presentation)
        {
            if (variantCatalog == null)
            {
                return false;
            }

            var baseEntry = baselineEmojis.FirstOrDefault(entry => entry.Id == presentation.Id);
            if (baseEntry == null)
            {
                return false;
            }

            var overrides = variantCatalog.GetVariantOverrides(baseEntry);
            if (overrides.Count == 0)
            {
                return false;
            }

            HidePreview();
            var menu = new ContextMenu
            {
                PlacementTarget = item,
            };
            foreach (var variant in overrides)
            {
                var selection = variantCatalog.Resolve(baseEntry, currentSkinTone, variant.Id);
                var menuItem = new MenuItem
                {
                    Header = variant.Name,
                    ToolTip = variant.CanonicalSequence,
                    Tag = selection,
                    Icon = new NotoEmojiImage
                    {
                        AssetPath = variant.AssetPath,
                        DecodeSizeDip = 24,
                        Width = 24,
                        Height = 24,
                    },
                };
                menuItem.Click += (_, _) => CommitEmoji(selection.ToPresentation(), CommitGesture.Pointer);
                menu.Items.Add(menuItem);
            }

            variantMenuOpen = true;
            menu.Closed += (_, _) => variantMenuOpen = false;
            item.ContextMenu = menu;
            menu.IsOpen = true;
            return true;
        }

        private void OpenSelectedVariantOverrideMenu()
        {
            if (EmojiGrid.SelectedItem is not Emoji emoji)
            {
                return;
            }

            EmojiGrid.ScrollIntoView(emoji);
            EmojiGrid.UpdateLayout();
            if (EmojiGrid.ItemContainerGenerator.ContainerFromItem(emoji) is ListBoxItem item)
            {
                OpenVariantOverrideMenu(item, emoji);
            }
        }

        private void EmojiItem_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is not ListBoxItem { DataContext: Emoji emoji } target)
            {
                return;
            }

            previewCloseTimer.Stop();
            OpenPreview(emoji, target, PreviewOrigin.Pointer);
        }

        private void EmojiItem_MouseLeave(object sender, MouseEventArgs e)
        {
            if (previewOrigin == PreviewOrigin.Pointer && sender == EmojiPreviewPopup.PlacementTarget)
            {
                SchedulePreviewClose();
            }
        }

        private void SchedulePreviewClose()
        {
            previewCloseTimer.Stop();
            previewCloseTimer.Start();
        }

        private void OpenKeyboardPreview()
        {
            var emoji = Keyboard.FocusedElement is ListBoxItem { DataContext: Emoji focused }
                ? focused
                : EmojiGrid.SelectedItem as Emoji;
            if (emoji == null)
            {
                return;
            }

            EmojiGrid.ScrollIntoView(emoji);
            EmojiGrid.UpdateLayout();
            var target = EmojiGrid.ItemContainerGenerator.ContainerFromItem(emoji) as ListBoxItem;
            if (target != null)
            {
                OpenPreview(emoji, target, PreviewOrigin.Keyboard);
            }
        }

        private void OpenPreview(Emoji emoji, ListBoxItem target, PreviewOrigin origin)
        {
            previewCloseTimer.Stop();
            previewOrigin = origin;

            var wasOpen = EmojiPreviewPopup.IsOpen;
            var previousTarget = EmojiPreviewPopup.PlacementTarget;
            System.Windows.Point? targetScreenOrigin = PresentationSource.FromVisual(target) != null
                ? target.PointToScreen(new System.Windows.Point(0, 0))
                : null;
            EmojiPreviewPopup.PlacementTarget = target;
            PreviewArtwork.AssetPath = emoji.PreviewAssetPath;
            PreviewLocalizedName.Text = emoji.Name;
            PreviewEnglishName.Text = emoji.EnglishName;
            PreviewEnglishName.Visibility = string.Equals(
                emoji.Name,
                emoji.EnglishName,
                StringComparison.CurrentCultureIgnoreCase)
                ? Visibility.Collapsed
                : Visibility.Visible;
            PreviewEmojiVersion.Text = $"Emoji {emoji.EmojiVersion}";
            System.Windows.Automation.AutomationProperties.SetName(
                EmojiPreviewPopup.Child,
                $"{emoji.Name}, Emoji {emoji.EmojiVersion}");
            if (!wasOpen)
            {
                EmojiPreviewPopup.IsOpen = true;
            }
            else if (!ReferenceEquals(previousTarget, target) &&
                previewTargetScreenOrigin is { } previousScreenOrigin &&
                targetScreenOrigin is { } currentScreenOrigin)
            {
                MoveOpenPreviewWithTarget(previousScreenOrigin, currentScreenOrigin);
            }

            previewTargetScreenOrigin = targetScreenOrigin;
        }

        private void MoveOpenPreviewWithTarget(
            System.Windows.Point previousTargetOrigin,
            System.Windows.Point currentTargetOrigin)
        {
            // WPF updates PlacementTarget while Popup is open, but its native window
            // keeps the previous screen position. Move that same non-activating window
            // by the target delta so content and position change without close/open
            // flicker. PointToScreen and GetWindowRect both use physical pixels.
            if (EmojiPreviewPopup.Child is not UIElement child ||
                PresentationSource.FromVisual(child) is not HwndSource popupSource ||
                popupSource.Handle == IntPtr.Zero ||
                !NativeMethods.GetWindowRect(popupSource.Handle, out var popupRect))
            {
                return;
            }

            var deltaX = (int)Math.Round(currentTargetOrigin.X - previousTargetOrigin.X);
            var deltaY = (int)Math.Round(currentTargetOrigin.Y - previousTargetOrigin.Y);
            NativeMethods.SetWindowPos(
                popupSource.Handle,
                IntPtr.Zero,
                popupRect.Left + deltaX,
                popupRect.Top + deltaY,
                0,
                0,
                NativeMethods.SwpNoSize |
                NativeMethods.SwpNoZOrder |
                NativeMethods.SwpNoActivate |
                NativeMethods.SwpNoOwnerZOrder);
        }

        private void HidePreview()
        {
            previewCloseTimer?.Stop();
            previewOrigin = PreviewOrigin.None;
            if (EmojiPreviewPopup != null)
            {
                EmojiPreviewPopup.IsOpen = false;
            }

            previewTargetScreenOrigin = null;
        }

        internal bool OpenSelectedPreviewForSmoke()
        {
            OpenKeyboardPreview();
            return EmojiPreviewPopup.IsOpen;
        }

        internal void ClosePreviewForSmoke() => HidePreview();

        internal bool OpenPointerPreviewForSmoke(Emoji emoji, ListBoxItem target)
        {
            OpenPreview(emoji, target, PreviewOrigin.Pointer);
            return EmojiPreviewPopup.IsOpen;
        }

        internal void SchedulePointerPreviewCloseForSmoke() => SchedulePreviewClose();

        private void TabItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Select the category on mouse-up (like emoji commit), which lands even
            // when the picker only has attached-input focus rather than full activation
            if (sender is ListBoxItem { DataContext: EmojiCategory category })
            {
                e.Handled = true;
                var index = categories.FindIndex(item => item.Key == category.Key);
                if (index >= 0)
                {
                    CategoryTabs.SelectedIndex = index;
                }
            }
        }

        private void CommitEmoji(Emoji emoji, CommitGesture gesture)
        {
            HidePreview();
            var continueSession = PickerSessionState.ContinuesAfter(gesture);
            var snapshot = CaptureViewSnapshot();
            var work = new InsertionWorkItem(emoji, snapshot, gesture);
            var enqueue = insertionQueue.Enqueue(work);
            if (enqueue.Status == QueueEnqueueStatus.Full)
            {
                UpdateInsertionQueueStatus(queueFullAttempted: true);
                AnnounceStatus(
                    $"Insertion queue is full with {enqueue.Capacity} waiting. Please wait before selecting another emoji.",
                    busy: true);
                return;
            }

            if (enqueue.Status == QueueEnqueueStatus.Stopped)
            {
                AnnounceStatus("The Picker is closing and cannot accept another emoji.", busy: true);
                return;
            }

            Logger.Log($"CommitEmoji queued: gesture={gesture}; pending={enqueue.PendingCount}; targetCaptured={App.PreviousForegroundWindow != IntPtr.Zero}");
            RecordActivity(emoji);
            ActivityNoticePanel.Visibility = Visibility.Collapsed;
            HideInsertionError();
            if (!continueSession)
            {
                insertionQueue.StopAfterDrain(QueueTerminalIntent.AfterCommit());
            }

            UpdateInsertionQueueStatus();
            AnnounceStatus(
                enqueue.PendingCount == 1
                    ? $"Queued {emoji.Name} for sending."
                    : $"Queued {emoji.Name}. {enqueue.PendingCount} pending.",
                busy: true);

            // Background priority deliberately lets already-posted rapid click/key
            // events enter the bounded FIFO before the first adapter hides the shell.
            ScheduleInsertionPump(DispatcherPriority.Background);
        }

        private void ScheduleInsertionPump(DispatcherPriority priority, bool replacePending = false)
        {
            if (insertionPumpRunning)
            {
                return;
            }

            if (insertionPumpOperation is { Status: DispatcherOperationStatus.Pending } pendingOperation)
            {
                if (!replacePending)
                {
                    return;
                }

                pendingOperation.Abort();
            }

            insertionPumpOperation = Dispatcher.BeginInvoke(new Action(async () =>
            {
                insertionPumpOperation = null;
                await PumpInsertionQueueAsync();
            }), priority);
        }

        private async Task PumpInsertionQueueAsync()
        {
            if (insertionPumpRunning)
            {
                return;
            }

            insertionPumpRunning = true;
            SetPointerActivationSuppressed(true);
            try
            {
                while (insertionQueue.TryStartNext(out var work) && work != null)
                {
                    insertionInProgress = true;
                    UpdateInsertionQueueStatus();
                    AnnounceStatus(
                        insertionQueue.PendingCount == 0
                            ? $"Sending {work.Emoji.Name}."
                            : $"Sending {work.Emoji.Name}. {insertionQueue.PendingCount} pending.",
                        busy: true);
                    if (PickerSessionState.ShouldHideDuringInsertion(work.Gesture))
                    {
                        Hide();
                    }

                    InsertionResult result;
                    try
                    {
                        // Every FIFO item independently revalidates the one target
                        // captured before this Picker Session. Failure never retargets.
                        result = await TextInjector.TryInsertAsync(
                            App.PreviousForegroundWindow,
                            App.PreviousFocusWindow,
                            work.Emoji.Character,
                            App.PreviousAccessibilityFocus);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogAlways($"Insert threw: {ex.GetType().Name}");
                        result = InsertionResult.Failure("The emoji could not be sent safely.");
                    }

                    insertionInProgress = false;
                    insertionQueue.CompleteActive();
                    lastInsertionSnapshot = work.Snapshot;

                    if (!result.Accepted &&
                        insertionQueue.TerminalIntent?.Kind is not QueueTerminalKind.Dismiss and
                        not QueueTerminalKind.TypingHandoff)
                    {
                        Logger.Log("Queued insert failed without retry or retarget");
                        var cancelled = insertionQueue.CancelPendingAndStop();
                        Logger.Log($"Insertion failure cancelled {cancelled} not-started item(s)");
                        insertionQueue.Reset();
                        UpdateInsertionQueueStatus();
                        SetPointerActivationSuppressed(false);
                        ShowInsertionError(
                            work.Emoji.Character,
                            result.Message ?? "The emoji could not be sent safely.",
                            work.Snapshot);
                        lastInsertionSnapshot = null;
                        return;
                    }
                }

                if (insertionQueue.IsTerminalReady)
                {
                    SetPointerActivationSuppressed(false);
                    await FinalizeQueueTerminationAsync();
                    return;
                }

                if (lastInsertionSnapshot != null)
                {
                    var completedSnapshot = lastInsertionSnapshot;
                    var completedName = (EmojiGrid.SelectedItem as Emoji)?.Name ?? "emoji";
                    insertionQueue.Reset();
                    lastInsertionSnapshot = null;
                    UpdateInsertionQueueStatus();
                    SetPointerActivationSuppressed(false);

                    // The user may choose another window while the Picker is hidden
                    // for insertion. Never reactivate the Picker over that explicit
                    // focus change.
                    if (NativeMethods.GetForegroundWindow() != App.PreviousForegroundWindow)
                    {
                        Logger.Log("Insertion completed after foreground changed; Picker remains dismissed");
                        FinalizeHiddenDismiss(returnFocusToTarget: false);
                        return;
                    }

                    RestorePickerAfterCommit(completedSnapshot);
                    AnnounceStatus($"Sent {completedName}. Picker remains open.", busy: false);
                }
            }
            finally
            {
                SetPointerActivationSuppressed(false);
                insertionInProgress = false;
                insertionPumpRunning = false;
            }
        }

        private void SetPointerActivationSuppressed(bool suppressed)
        {
            if (pointerActivationSuppressed == suppressed)
            {
                return;
            }

            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle).ToInt64();
            var updated = suppressed
                ? style | NativeMethods.WsExNoActivate
                : style & ~NativeMethods.WsExNoActivate;
            NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, new IntPtr(updated));
            pointerActivationSuppressed = suppressed;
        }

        private void BeginTypingHandoff(string committedText)
        {
            BeginTypingHandoff(TypingHandoffPayload.Text(committedText));
        }

        private void BeginTypingHandoff(TypingHandoffPayload payload)
        {
            HidePreview();
            HideInsertionError();
            var cancelled = insertionQueue.StopAndCancelPending(
                QueueTerminalIntent.TypingHandoff(payload));
            Logger.Log($"Typing Handoff started; cancelled {cancelled} not-started insertion(s)");
            UpdateInsertionQueueStatus();
            AnnounceStatus("Returning the first input to the original target.", busy: true);
            Hide();

            // Input priority prevents another desktop key message overtaking target
            // activation. Only one committed TextInput or exact shortcut chord is
            // buffered and handed to the validated captured target.
            ScheduleInsertionPump(DispatcherPriority.Input, replacePending: true);
        }

        private async Task FinalizeQueueTerminationAsync()
        {
            var intent = insertionQueue.TerminalIntent
                ?? throw new InvalidOperationException("A terminal queue has no terminal intent.");

            if (intent.Kind == QueueTerminalKind.TypingHandoff)
            {
                var payload = intent.Handoff
                    ?? throw new InvalidOperationException("Typing Handoff has no payload.");
                InsertionResult handoff;
                try
                {
                    handoff = payload.Kind == TypingHandoffKind.CommittedText
                        ? await TextInjector.TryInsertAsync(
                            App.PreviousForegroundWindow,
                            App.PreviousFocusWindow,
                            payload.CommittedText ?? throw new InvalidOperationException("Typing Handoff has no committed text."),
                            App.PreviousAccessibilityFocus)
                        : await TextInjector.TrySendKeyStrokeAsync(
                            App.PreviousForegroundWindow,
                            App.PreviousFocusWindow,
                            payload.VirtualKey,
                            payload.Modifiers,
                            App.PreviousAccessibilityFocus);
                }
                catch (Exception ex)
                {
                    Logger.LogAlways($"Typing Handoff threw: {ex.GetType().Name}");
                    handoff = InsertionResult.Failure("The first typed input could not be handed off safely.");
                }

                insertionQueue.Reset();
                UpdateInsertionQueueStatus();
                if (!handoff.Accepted)
                {
                    ShowInsertionError(
                        payload.Kind == TypingHandoffKind.CommittedText ? payload.CommittedText : null,
                        handoff.Message ?? "The first typed input could not be handed off safely.",
                        lastInsertionSnapshot ?? CaptureViewSnapshot());
                    lastInsertionSnapshot = null;
                    return;
                }

                lastInsertionSnapshot = null;
                FinalizeHiddenDismiss(returnFocusToTarget: false);
                return;
            }

            var returnFocus = intent.ReturnFocusToTarget;
            insertionQueue.Reset();
            lastInsertionSnapshot = null;
            UpdateInsertionQueueStatus();
            FinalizeHiddenDismiss(returnFocus);
        }

        private static ShortcutModifiers GetShortcutModifiers()
        {
            var modifiers = ShortcutModifiers.None;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            {
                modifiers |= ShortcutModifiers.Control;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
            {
                modifiers |= ShortcutModifiers.Alt;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            {
                modifiers |= ShortcutModifiers.Shift;
            }

            if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
            {
                modifiers |= ShortcutModifiers.Windows;
            }

            return modifiers;
        }

        private void UpdateInsertionQueueStatus(bool queueFullAttempted = false)
        {
            var queueFull = queueFullAttempted || insertionQueue.IsFull;
            var sending = insertionQueue.Active is InsertionWorkItem;
            var typingHandoff = insertionQueue.TerminalIntent?.Kind == QueueTerminalKind.TypingHandoff;
            var status = InsertionQueuePresentation.VisibleStatus(
                queueFull,
                sending,
                insertionQueue.PendingCount,
                typingHandoff);
            var accessibleStatus = InsertionQueuePresentation.AccessibleStatus(
                queueFull,
                sending,
                insertionQueue.PendingCount,
                typingHandoff);

            InsertionQueueStatusText.Text = status ?? string.Empty;
            InsertionQueueStatusText.Visibility = status == null ? Visibility.Collapsed : Visibility.Visible;
            EmojiGrid.IsHitTestVisible = insertionQueue.IsAccepting && !insertionQueue.IsFull;
            if (accessibleStatus != null)
            {
                System.Windows.Automation.AutomationProperties.SetItemStatus(EmojiGrid, $"Busy. {accessibleStatus}");
            }
            else
            {
                System.Windows.Automation.AutomationProperties.SetItemStatus(EmojiGrid, string.Empty);
            }
        }

        private PickerViewSnapshot CaptureViewSnapshot()
        {
            var scroll = FindVisualChild<ScrollViewer>(EmojiGrid)?.VerticalOffset ?? 0;
            return new PickerViewSnapshot(
                sessionState.Mode,
                SearchBox.Text,
                currentCategory,
                (EmojiGrid.SelectedItem as Emoji)?.Character,
                scroll);
        }

        private void RestorePickerAfterCommit(PickerViewSnapshot snapshot)
        {
            currentCategory = snapshot.Category;
            if (!string.Equals(SearchBox.Text, snapshot.Query, StringComparison.Ordinal))
            {
                SearchBox.Text = snapshot.Query;
            }

            isShowing = true;
            Show();
            Activate();
            var handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                ForceForeground(handle);
            }

            if (snapshot.Mode == PickerInputMode.Search)
            {
                sessionState.EnterSearch();
                SearchBox.Focus();
                Keyboard.Focus(SearchBox);
            }
            else
            {
                FocusBrowseGrid();
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                var selected = EmojiGrid.Items.Cast<Emoji>().FirstOrDefault(item =>
                    string.Equals(item.Character, snapshot.SelectedCharacter, StringComparison.Ordinal));
                if (selected != null)
                {
                    EmojiGrid.SelectedItem = selected;
                    EmojiGrid.ScrollIntoView(selected);
                }

                FindVisualChild<ScrollViewer>(EmojiGrid)?.ScrollToVerticalOffset(snapshot.VerticalOffset);
                isShowing = false;
            }), DispatcherPriority.Loaded);
        }

        private void ShowInsertionError(Emoji emoji, string message, PickerViewSnapshot? snapshot = null)
        {
            ShowInsertionError(emoji.Character, message, snapshot);
            System.Windows.Automation.AutomationProperties.SetName(
                ExplicitCopyButton,
                "Copy selected emoji to clipboard");
        }

        private void ShowInsertionError(string? recoverableText, string message, PickerViewSnapshot? snapshot = null)
        {
            failedInsertionText = recoverableText;
            InsertionErrorText.Text = message;
            InsertionErrorPanel.Visibility = Visibility.Visible;
            System.Windows.Automation.AutomationProperties.SetName(
                ExplicitCopyButton,
                "Copy unsent text to clipboard");
            ExplicitCopyButton.Visibility = recoverableText == null ? Visibility.Collapsed : Visibility.Visible;

            // The picker was hidden before target activation. Bring the same shell
            // back without resetting query/category/selection/scroll.
            RestorePickerAfterCommit(snapshot ?? CaptureViewSnapshot());
            AnnounceStatus($"Error. {message}", busy: false);
        }

        private void HideInsertionError()
        {
            failedInsertionText = null;
            InsertionErrorPanel.Visibility = Visibility.Collapsed;
            InsertionErrorText.Text = string.Empty;
            ExplicitCopyButton.Visibility = Visibility.Visible;
            System.Windows.Automation.AutomationProperties.SetName(
                ExplicitCopyButton,
                "Copy selected emoji to clipboard");
        }

        private void ExplicitCopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (failedInsertionText == null)
            {
                return;
            }

            if (TextInjector.CopyExplicit(failedInsertionText))
            {
                InsertionErrorText.Text = "Copied to the clipboard. It will appear in clipboard history when enabled.";
            }
            else
            {
                InsertionErrorText.Text = "The emoji could not be copied to the clipboard.";
            }
        }

        /// <summary>
        /// Hides the resident picker (it is reused on the next hotkey press)
        /// and persists the recents list. A repeated hotkey is handled as a no-op
        /// by the resident application rather than toggling this session closed.
        /// </summary>
        public void DismissPicker() => DismissPicker(PickerDismissReason.CloseButton);

        private void DismissPicker(PickerDismissReason reason)
        {
            searchTimer.Stop(); // no point filtering a hidden grid
            HidePreview();
            if (insertionQueue.HasWork || insertionPumpRunning || insertionQueue.TerminalIntent != null ||
                insertionPumpOperation is { Status: DispatcherOperationStatus.Pending })
            {
                var cancelled = insertionQueue.StopAndCancelPending(QueueTerminalIntent.Dismiss(reason));
                Logger.Log($"Dismiss requested; cancelled {cancelled} not-started insertion(s)");
                UpdateInsertionQueueStatus();
                Hide();
                ScheduleInsertionPump(DispatcherPriority.Input, replacePending: true);
                return;
            }

            FinalizeHiddenDismiss(PickerSessionState.ReturnsFocusAfter(reason));
        }

        private void FinalizeHiddenDismiss(bool returnFocusToTarget)
        {
            PersistSessionState();
            Hide();

            if (returnFocusToTarget)
            {
                TextInjector.TryRestoreCapturedTarget(
                    App.PreviousForegroundWindow,
                    App.PreviousFocusWindow,
                    App.PreviousAccessibilityFocus);
            }

            // Give the memory back while we idle in the tray; ContextIdle runs
            // after the hide (and any pending insertion) has fully settled
            Dispatcher.BeginInvoke(new Action(MemoryTrimmer.Trim), DispatcherPriority.ContextIdle);

            var exit = processExitAfterQueue;
            processExitAfterQueue = null;
            if (exit != null)
            {
                PrepareForProcessExit();
                exit();
            }
        }

        public void RequestProcessExit(Action exit)
        {
            ArgumentNullException.ThrowIfNull(exit);
            if (insertionQueue.HasWork || insertionPumpRunning ||
                insertionPumpOperation is { Status: DispatcherOperationStatus.Pending })
            {
                processExitAfterQueue = exit;
                DismissPicker(PickerDismissReason.ProcessExit);
                return;
            }

            PrepareForProcessExit();
            exit();
        }

        /// <summary>
        /// Allows the tray Exit command to close the reusable WPF window while
        /// keeping every ordinary close gesture as a non-destructive dismissal.
        /// </summary>
        public void PrepareForProcessExit()
        {
            PersistSessionState();
            allowProcessExit = true;
        }

        private void PersistSessionState()
        {
            if (persistUserActivity && IsLoaded && Left > -30000)
            {
                Settings.SetPickerSize(ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height);
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!allowProcessExit)
            {
                e.Cancel = true;
                DismissPicker(PickerDismissReason.CloseButton);
                return;
            }

            base.OnClosing(e);
        }

        private void MainWindow_Deactivated(object sender, EventArgs e)
        {
            if (variantMenuOpen)
            {
                Logger.Log("Deactivated ignored (variant menu open)");
                return;
            }

            if (insertionInProgress || insertionQueue.TerminalIntent != null)
            {
                Logger.Log("Deactivated ignored (insertion queue or handoff in progress)");
                return;
            }

            if (insertionQueue.HasWork)
            {
                Logger.Log("Deactivated with pending insertion -> cancel and respect external focus");
                DismissPicker(PickerDismissReason.ExternalPointer);
                return;
            }

            // Ignore the transient deactivation that can occur while we are still
            // bringing the window to the foreground, or it would hide immediately
            if (isShowing)
            {
                Logger.Log("Deactivated ignored (still showing)");
                return;
            }

            // Dismiss when focus leaves the panel, like the Windows 10 picker
            Logger.Log("Deactivated -> dismiss");
            DismissPicker(PickerDismissReason.ExternalPointer);
        }

        private void RecordActivity(Emoji emoji)
        {
            // Selection is Activity Data regardless of the later insertion
            // outcome. Emoji.Id is the stable base entry; the resolved ID and
            // Unicode sequence preserve the exact chosen skin-tone/override.
            activityData.RecordSelection(emoji.Id, emoji.ResolvedEntryId, emoji.Character);
            RefreshRecentEmojis();
            Logger.Log($"RecordActivity -> recents now {recentEmojis.Count}");
        }

        private void RefreshRecentEmojis()
        {
            recentEmojis = activityData.RecentEntries
                .Select(saved => variantCatalog?.TryRestore(saved.ResolvedEntryId, saved.UnicodeSequence))
                .OfType<EmojiSelection>()
                .Select(selection => selection.ToPresentation())
                .ToList();
            categoryDataVersion++;
        }

        private void ShowActivityRecoveryNotice()
        {
            if (activityData.RecoveryNotices.Count == 0)
            {
                return;
            }

            var thai = Localizer.IsThai;
            ActivityNoticeText.Text = thai
                ? "ข้อมูลกิจกรรมบางส่วนอ่านไม่ได้ ระบบสำรองไฟล์เดิมและรีเซ็ตเฉพาะส่วนนั้นแล้ว"
                : "Some activity data was unreadable. The original was backed up and only that part was reset.";
            ActivityNoticePanel.Visibility = Visibility.Visible;
        }

        internal void ClearRecentActivity()
        {
            activityData.ClearRecent();
            RefreshRecentEmojis();
            if (currentCategory == RecentCategoryKey && string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                LoadCategory(RecentCategoryKey);
            }
        }

        internal void ResetLearnedRanking() => activityData.ResetLearnedRanking();

        internal void ClearAllActivity()
        {
            activityData.ClearAllActivity();
            RefreshRecentEmojis();
            if (currentCategory == RecentCategoryKey && string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                LoadCategory(RecentCategoryKey);
            }
        }

        private void ActivityNoticeDismissButton_Click(object sender, RoutedEventArgs e) =>
            ActivityNoticePanel.Visibility = Visibility.Collapsed;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DismissPicker(PickerDismissReason.CloseButton);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Handled)
            {
                base.OnKeyDown(e);
                return;
            }

            if (e.Key == Key.Escape)
            {
                var outcome = sessionState.Escape();
                if (!outcome.Dismiss)
                {
                    SearchBox.Clear(); // TextChanged restores the category
                    FocusBrowseGrid();
                    AnnounceStatus("Browse mode. Press Escape again to close.", busy: false);
                }
                else
                {
                    DismissPicker(PickerDismissReason.Escape);
                }

                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        private enum PreviewOrigin
        {
            None,
            Pointer,
            Keyboard,
        }

        private sealed record InsertionWorkItem(
            Emoji Emoji,
            PickerViewSnapshot Snapshot,
            CommitGesture Gesture);

        private sealed record PickerViewSnapshot(
            PickerInputMode Mode,
            string Query,
            string Category,
            string? SelectedCharacter,
            double VerticalOffset);

    }

    public class Emoji
    {
        public string Id { get; }
        public string Character { get; }
        public string Name => Localizer.IsThai ? ThaiName : EnglishName;
        public string EnglishName { get; }
        public string ThaiName { get; }
        public string Category { get; }
        public string CanonicalSequence { get; }
        public string EmojiVersion { get; }
        public string AssetPath { get; }
        public string PreviewAssetPath { get; }
        public int Order { get; }
        public string BaseCanonicalSequence { get; }
        public string ResolvedEntryId { get; }
        public bool IsVariantOverride { get; }

        public IReadOnlyList<string> EnglishKeywords { get; }
        public IReadOnlyList<string> ThaiKeywords { get; }

        /// <summary>Combined CLDR search terms retained for diagnostics and compatibility.</summary>
        public string Keywords { get; }

        /// <summary>Usage-popularity tier from Unicode's frequency data
        /// (0 = most used); unranked emoji get a large sentinel value.</summary>
        public int Popularity { get; set; }

        public Emoji(
            string id,
            string character,
            string name,
            string englishName,
            string thaiName,
            string category,
            string canonicalSequence,
            IReadOnlyList<string> englishKeywords,
            IReadOnlyList<string> thaiKeywords,
            string emojiVersion,
            string assetPath,
            string previewAssetPath,
            int order,
            int popularity,
            string? baseCanonicalSequence = null,
            string? resolvedEntryId = null,
            bool isVariantOverride = false)
        {
            Id = id;
            Character = character;
            _ = name; // retained in the generated-data constructor contract
            EnglishName = englishName;
            ThaiName = thaiName;
            Category = category;
            CanonicalSequence = canonicalSequence;
            EnglishKeywords = englishKeywords;
            ThaiKeywords = thaiKeywords;
            Keywords = string.Join(' ', englishKeywords.Concat(thaiKeywords));
            EmojiVersion = emojiVersion;
            AssetPath = assetPath;
            PreviewAssetPath = previewAssetPath;
            Order = order;
            Popularity = popularity;
            BaseCanonicalSequence = baseCanonicalSequence ?? canonicalSequence;
            ResolvedEntryId = resolvedEntryId ?? id;
            IsVariantOverride = isVariantOverride;
        }

        // Shown by UI Automation / screen readers for the grid items
        public override string ToString() => Name;
    }

    public class EmojiCategory
    {
        public string Key { get; }
        public string IconAssetPath { get; }
        public string DisplayName { get; }

        public EmojiCategory(string key, string iconAssetPath, string displayName)
        {
            Key = key;
            IconAssetPath = iconAssetPath;
            DisplayName = displayName;
        }
    }

    internal sealed record SkinToneOption(
        SkinTonePreference Preference,
        string DisplayName,
        string IconAssetPath = "");
}
