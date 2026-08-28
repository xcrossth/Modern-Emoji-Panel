using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EmojiPicker;

/// <summary>
/// Decodes bundled Noto PNG files on demand. The cache is deliberately bounded:
/// a full catalog browse must never retain or decode all artwork at startup.
/// </summary>
internal sealed class NotoEmojiAssetProvider
{
    private const int MaximumCachedImages = 256;
    private readonly object gate = new();
    private readonly Dictionary<CacheKey, CacheValue> cache = [];
    private readonly Dictionary<CacheKey, Task<ImageSource?>> inFlight = [];
    private readonly LinkedList<CacheKey> leastRecentlyUsed = [];

    public static NotoEmojiAssetProvider Shared { get; } = new();

    internal int CachedImageCount
    {
        get
        {
            lock (gate)
            {
                return cache.Count;
            }
        }
    }

    public Task<ImageSource?> LoadAsync(string relativePath, int decodePixelWidth)
    {
        var key = new CacheKey(relativePath, Math.Clamp(decodePixelWidth, 16, 512));
        lock (gate)
        {
            if (cache.TryGetValue(key, out var cached))
            {
                Touch(cached.Node);
                return Task.FromResult(cached.Image);
            }

            if (inFlight.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var request = Task.Run(() => Decode(key));
            inFlight.Add(key, request);
            _ = request.ContinueWith(
                completed => CompleteLoad(key, completed.Status == TaskStatus.RanToCompletion ? completed.Result : null),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return request;
        }
    }

    private static ImageSource? Decode(CacheKey key)
    {
        try
        {
            var path = EmojiCatalog.ResolveBundledPath(key.RelativePath);
            var assetRoot = Path.GetFullPath(EmojiCatalog.AssetRoot) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(assetRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.DecodePixelWidth = key.DecodePixelWidth;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void CompleteLoad(CacheKey key, ImageSource? image)
    {
        lock (gate)
        {
            inFlight.Remove(key);
            if (cache.ContainsKey(key))
            {
                return;
            }

            var node = leastRecentlyUsed.AddFirst(key);
            cache.Add(key, new CacheValue(image, node));
            while (cache.Count > MaximumCachedImages)
            {
                var last = leastRecentlyUsed.Last!;
                leastRecentlyUsed.RemoveLast();
                cache.Remove(last.Value);
            }
        }
    }

    private void Touch(LinkedListNode<CacheKey> node)
    {
        leastRecentlyUsed.Remove(node);
        leastRecentlyUsed.AddFirst(node);
    }

    private readonly record struct CacheKey(string RelativePath, int DecodePixelWidth);
    private sealed record CacheValue(ImageSource? Image, LinkedListNode<CacheKey> Node);
}
