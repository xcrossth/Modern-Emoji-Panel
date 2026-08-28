using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EmojiPicker;

/// <summary>
/// An image that requests its bitmap only while WPF realizes it. Recycled item
/// containers update AssetPath, so virtual scrolling naturally limits decoding
/// to the visible and near-viewport tiles.
/// </summary>
internal sealed class NotoEmojiImage : System.Windows.Controls.Image
{
    public static readonly DependencyProperty AssetPathProperty = DependencyProperty.Register(
        nameof(AssetPath),
        typeof(string),
        typeof(NotoEmojiImage),
        new PropertyMetadata(string.Empty, OnAssetRequestChanged));

    public static readonly DependencyProperty DecodeSizeDipProperty = DependencyProperty.Register(
        nameof(DecodeSizeDip),
        typeof(double),
        typeof(NotoEmojiImage),
        new PropertyMetadata(32.0, OnAssetRequestChanged));

    private static readonly DependencyPropertyKey IsAssetUnavailablePropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsAssetUnavailable),
        typeof(bool),
        typeof(NotoEmojiImage),
        new PropertyMetadata(false));

    public static readonly DependencyProperty IsAssetUnavailableProperty = IsAssetUnavailablePropertyKey.DependencyProperty;

    private Window? ownerWindow;
    private long requestVersion;

    public NotoEmojiImage()
    {
        Stretch = Stretch.Uniform;
        SnapsToDevicePixels = true;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string AssetPath
    {
        get => (string)GetValue(AssetPathProperty);
        set => SetValue(AssetPathProperty, value);
    }

    public double DecodeSizeDip
    {
        get => (double)GetValue(DecodeSizeDipProperty);
        set => SetValue(DecodeSizeDipProperty, value);
    }

    public bool IsAssetUnavailable => (bool)GetValue(IsAssetUnavailableProperty);

    internal static int CalculateDecodePixelWidth(double sizeDip, double dpiScale) =>
        Math.Max(1, (int)Math.Ceiling(sizeDip * dpiScale));

    private static void OnAssetRequestChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is NotoEmojiImage image && image.IsLoaded)
        {
            image.RequestImage();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ownerWindow = Window.GetWindow(this);
        if (ownerWindow != null)
        {
            ownerWindow.DpiChanged += OwnerWindow_DpiChanged;
        }

        RequestImage();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        requestVersion++;
        if (ownerWindow != null)
        {
            ownerWindow.DpiChanged -= OwnerWindow_DpiChanged;
            ownerWindow = null;
        }
    }

    private void OwnerWindow_DpiChanged(object sender, DpiChangedEventArgs e) => RequestImage();

    private async void RequestImage()
    {
        var currentRequest = ++requestVersion;
        Source = null;
        SetValue(IsAssetUnavailablePropertyKey, false);
        if (string.IsNullOrWhiteSpace(AssetPath))
        {
            SetValue(IsAssetUnavailablePropertyKey, true);
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var decodeWidth = CalculateDecodePixelWidth(DecodeSizeDip, dpi.DpiScaleX);
        var source = await NotoEmojiAssetProvider.Shared.LoadAsync(AssetPath, decodeWidth);
        if (currentRequest != requestVersion || !IsLoaded)
        {
            return;
        }

        Source = source;
        SetValue(IsAssetUnavailablePropertyKey, source == null);
    }
}
