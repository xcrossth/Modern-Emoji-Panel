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
    public partial class MainWindow : Window
    {
        private const int MaxRecentEmojis = 24;
        private const string RecentCategoryKey = "Recent";
        private const string SearchHeader = "Search results";

        // Emoji cell footprint in DIPs (40x40 border + 1px margin each side);
        // used to derive the grid's current column count for keyboard nav
        private const double ItemCellWidth = 42.0;

        // How long to wait after the last keystroke before filtering, so typing
        // stays smooth instead of re-rendering the grid on every character
        private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(120);
        internal static readonly TimeSpan HoverPreviewDelay = TimeSpan.FromMilliseconds(400);

        private static readonly string RecentEmojisFile = Path.Combine(
            ProductIdentity.DataDirectory,
            "recent.json");

        private const string DefaultCategoryKey = "Smileys & Emotion";

        private readonly DispatcherTimer searchTimer;
        private readonly DispatcherTimer previewTimer;
        private readonly bool persistUserActivity;
        private EmojiSearchIndex searchIndex;
        private List<Emoji> baselineEmojis = new List<Emoji>();
        private List<Emoji> allEmojis = new List<Emoji>();
        private List<Emoji> recentEmojis = new List<Emoji>();
        private List<EmojiCategory> categories = new List<EmojiCategory>();
        private string currentCategory = DefaultCategoryKey;
        private bool bundledAssetsAvailable;
        private bool isShowing;
        private bool recentsDirty;
        private bool allowProcessExit;
        private Emoji? failedInsertionEmoji;
        private Emoji? pendingPreviewEmoji;
        private ListBoxItem? pendingPreviewTarget;
        private PreviewOrigin previewOrigin;
        private EmojiVariantCatalog? variantCatalog;
        private SkinTonePreference currentSkinTone = SkinTonePreference.Neutral;
        private bool skinTonePickerReady;
        private bool variantMenuOpen;
        private readonly PickerSessionState sessionState = new();
        private bool insertionInProgress;

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
            searchIndex = new EmojiSearchIndex(allEmojis);
            InitializeSkinTonePicker();
            if (loadUserActivity)
            {
                LoadRecentEmojis();
            }

            searchTimer = new DispatcherTimer { Interval = SearchDebounce };
            searchTimer.Tick += (_, _) => RunSearch();
            previewTimer = new DispatcherTimer { Interval = HoverPreviewDelay };
            previewTimer.Tick += (_, _) => OpenPendingHoverPreview();

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
        internal bool ExplicitCopyAvailable => ExplicitCopyButton.IsEnabled && ExplicitCopyButton.Visibility == Visibility.Visible;
        internal void ShowInsertionFailureForSmoke(Emoji emoji, string message) => ShowInsertionError(emoji, message);
        internal bool IsPreviewOpen => EmojiPreviewPopup.IsOpen;
        internal string PreviewLocalizedNameText => PreviewLocalizedName.Text;
        internal string PreviewEnglishNameText => PreviewEnglishName.Text;
        internal string PreviewVersionText => PreviewEmojiVersion.Text;
        internal string PreviewAssetPath => PreviewArtwork.AssetPath;
        internal PickerInputMode InputMode => sessionState.Mode;
        internal bool IsPickerSessionOpen => IsVisible;
        internal string AccessibilityStatus => AutomationStatusText.Text;

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
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            isShowing = true;
            sessionState.Begin();

            HidePreview();
            SearchBox.Clear();

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

            PositionNearCursor();

            Show();
            EnsureOnScreen();
            Activate();
            var handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                ForceForeground(handle);
            }

            FocusBrowseGrid();
            AnnounceStatus("Browse mode. Use arrow keys to choose an emoji.", busy: false);

            Logger.Log($"ShowPicker done in {stopwatch.ElapsedMilliseconds}ms: Left={Left:F0} Top={Top:F0} " +
                $"W={Width} H={Height} foreground={NativeMethods.GetForegroundWindow()} thisHwnd={handle}");

            // Clear the guard once the show/activation storm has settled
            Dispatcher.BeginInvoke(new Action(() => isShowing = false), System.Windows.Threading.DispatcherPriority.Background);
        }

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
            // center on that same target window and monitor (not the mouse, which
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
        }

        private void InitializeSkinTonePicker()
        {
            var thai = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "th";
            var options = new List<SkinToneOption>
            {
                new(SkinTonePreference.Neutral, thai ? "กลาง (สีเหลือง)" : "Neutral (yellow)"),
                new(SkinTonePreference.Light, thai ? "สีผิวอ่อน" : "Light skin tone"),
                new(SkinTonePreference.MediumLight, thai ? "สีผิวขาวเหลือง" : "Medium-light skin tone"),
                new(SkinTonePreference.Medium, thai ? "สีผิวปานกลาง" : "Medium skin tone"),
                new(SkinTonePreference.MediumDark, thai ? "สีผิวเข้มปานกลาง" : "Medium-dark skin tone"),
                new(SkinTonePreference.Dark, thai ? "สีผิวเข้ม" : "Dark skin tone"),
            };

            SkinTonePicker.ItemsSource = options;
            SkinTonePicker.SelectedItem = options.Single(option => option.Preference == currentSkinTone);
            skinTonePickerReady = true;
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
            searchIndex = new EmojiSearchIndex(allEmojis);

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
            var thai = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "th";
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

            List<Emoji> categoryEmojis = categoryKey == RecentCategoryKey
                ? recentEmojis.ToList()
                : allEmojis.Where(emoji => emoji.Category == categoryKey).ToList();

            Logger.Log($"LoadCategory '{categoryKey}' -> {categoryEmojis.Count} items");
            CategoryHeader.Text = categories.FirstOrDefault(category => category.Key == categoryKey)?.DisplayName ?? categoryKey;
            ShowEmojis(categoryEmojis);
        }

        private void ShowEmojis(List<Emoji> emojis)
        {
            HidePreview();
            EmojiGrid.ItemsSource = emojis;
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

            if (e.Key == Key.Escape && (EmojiPreviewPopup.IsOpen || previewTimer.IsEnabled))
            {
                HidePreview();
                e.Handled = true;
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
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Win10 picker behaviour: typing searches while the arrow keys move
            // the grid selection and Enter commits it, wherever focus happens to be
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
                AnnounceStatus($"Selected {emoji.Name}.", busy: insertionInProgress);
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

            HidePreview();
            pendingPreviewEmoji = emoji;
            pendingPreviewTarget = target;
            previewTimer.Start();
        }

        private void EmojiItem_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender == pendingPreviewTarget ||
                (previewOrigin == PreviewOrigin.Pointer && sender == EmojiPreviewPopup.PlacementTarget))
            {
                HidePreview();
            }
        }

        private void OpenPendingHoverPreview()
        {
            previewTimer.Stop();
            if (pendingPreviewEmoji == null || pendingPreviewTarget == null || !pendingPreviewTarget.IsMouseOver)
            {
                pendingPreviewEmoji = null;
                pendingPreviewTarget = null;
                return;
            }

            OpenPreview(pendingPreviewEmoji, pendingPreviewTarget, PreviewOrigin.Pointer);
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
            previewTimer.Stop();
            pendingPreviewEmoji = null;
            pendingPreviewTarget = null;
            previewOrigin = origin;

            EmojiPreviewPopup.IsOpen = false;
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
            EmojiPreviewPopup.IsOpen = true;
        }

        private void HidePreview()
        {
            previewTimer?.Stop();
            pendingPreviewEmoji = null;
            pendingPreviewTarget = null;
            previewOrigin = PreviewOrigin.None;
            if (EmojiPreviewPopup != null)
            {
                EmojiPreviewPopup.IsOpen = false;
            }
        }

        internal bool OpenSelectedPreviewForSmoke()
        {
            OpenKeyboardPreview();
            return EmojiPreviewPopup.IsOpen;
        }

        internal void ClosePreviewForSmoke() => HidePreview();

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
            if (insertionInProgress)
            {
                AnnounceStatus("Busy sending the previous emoji.", busy: true);
                return;
            }

            HidePreview();
            var continueSession = PickerSessionState.ContinuesAfter(gesture);
            var snapshot = CaptureViewSnapshot();
            Logger.Log($"CommitEmoji: gesture={gesture} continue={continueSession} '{emoji.Character}' -> target={App.PreviousForegroundWindow}");
            AddToRecentEmojis(emoji);
            HideInsertionError();
            insertionInProgress = true;
            AnnounceStatus($"Sending {emoji.Name}.", busy: true);
            Hide();

            // Defer the insertion until the current input event has finished
            // processing: injecting keystrokes from inside a key/mouse handler
            // corrupts the injected Unicode sequence
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                // Insert only into the target captured before the picker opened.
                // Failure never retargets and never silently copies.
                InsertionResult result;
                try
                {
                    result = await TextInjector.TryInsertAsync(
                        App.PreviousForegroundWindow, App.PreviousFocusWindow, emoji.Character);
                }
                catch (Exception ex)
                {
                    Logger.LogAlways($"Insert threw: {ex}");
                    result = InsertionResult.Failure("The emoji could not be sent safely.");
                }

                insertionInProgress = false;
                if (!result.Accepted)
                {
                    Logger.Log($"Insert failed without retry or retarget: {result.Message}");
                    ShowInsertionError(
                        emoji,
                        result.Message ?? "The emoji could not be sent safely.",
                        snapshot);
                }
                else if (continueSession)
                {
                    RestorePickerAfterCommit(snapshot);
                    AnnounceStatus($"Sent {emoji.Name}. Picker remains open.", busy: false);
                }
                else
                {
                    PersistSessionState();
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
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
            failedInsertionEmoji = emoji;
            InsertionErrorText.Text = message;
            InsertionErrorPanel.Visibility = Visibility.Visible;

            // The picker was hidden before target activation. Bring the same shell
            // back without resetting query/category/selection/scroll.
            RestorePickerAfterCommit(snapshot ?? CaptureViewSnapshot());
            AnnounceStatus($"Error. {message}", busy: false);
        }

        private void HideInsertionError()
        {
            failedInsertionEmoji = null;
            InsertionErrorPanel.Visibility = Visibility.Collapsed;
            InsertionErrorText.Text = string.Empty;
        }

        private void ExplicitCopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (failedInsertionEmoji == null)
            {
                return;
            }

            if (TextInjector.CopyExplicit(failedInsertionEmoji.Character))
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
            PersistSessionState();
            Hide();

            if (PickerSessionState.ReturnsFocusAfter(reason))
            {
                TextInjector.TryRestoreCapturedTarget(App.PreviousForegroundWindow, App.PreviousFocusWindow);
            }

            // Give the memory back while we idle in the tray; ContextIdle runs
            // after the hide (and any pending insertion) has fully settled
            Dispatcher.BeginInvoke(new Action(MemoryTrimmer.Trim), DispatcherPriority.ContextIdle);
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
            if (recentsDirty)
            {
                SaveRecentEmojis();
                recentsDirty = false;
            }

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

            if (insertionInProgress)
            {
                Logger.Log("Deactivated ignored (insertion in progress)");
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

        private void AddToRecentEmojis(Emoji emoji)
        {
            // Remove if already exists
            recentEmojis.RemoveAll(item => item.Character == emoji.Character);

            // Add to beginning
            recentEmojis.Insert(0, emoji);

            // Keep only the most recent MaxRecentEmojis
            if (recentEmojis.Count > MaxRecentEmojis)
            {
                recentEmojis.RemoveAt(recentEmojis.Count - 1);
            }

            recentsDirty = true;
            Logger.Log($"AddToRecent '{emoji.Character}' -> recents now {recentEmojis.Count}");
        }

        private void LoadRecentEmojis()
        {
            try
            {
                if (!File.Exists(RecentEmojisFile))
                {
                    return;
                }

                var characters = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(RecentEmojisFile));
                if (characters == null)
                {
                    return;
                }

                recentEmojis = characters
                    .Distinct() // a corrupt/legacy file could contain duplicates
                    .Select(character => baselineEmojis.FirstOrDefault(item => item.Character == character))
                    .OfType<Emoji>()
                    .Select(entry => variantCatalog!.RestoreResolved(entry).ToPresentation())
                    .Take(MaxRecentEmojis)
                    .ToList();
            }
            catch (Exception)
            {
                // A corrupt or unreadable recents file should never stop the app from starting
                recentEmojis = new List<Emoji>();
            }
        }

        private void SaveRecentEmojis()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RecentEmojisFile)!);
                var json = JsonSerializer.Serialize(recentEmojis.Select(item => item.Character));

                // Write to a temp file then atomically swap it in, so a crash or
                // power loss mid-write can't leave a truncated recent.json
                var tmp = RecentEmojisFile + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(RecentEmojisFile))
                {
                    File.Replace(tmp, RecentEmojisFile, null);
                }
                else
                {
                    File.Move(tmp, RecentEmojisFile);
                }
            }
            catch (Exception)
            {
                // Losing the recents list is not worth interrupting the user for
            }
        }

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
        public string Name { get; }
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
            Name = name;
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

    internal sealed record SkinToneOption(SkinTonePreference Preference, string DisplayName);
}
