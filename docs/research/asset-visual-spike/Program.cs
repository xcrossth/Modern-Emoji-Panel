using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class Program
{
    private const double GridDip = 32.0;
    private const int TimingPasses = 24;

    private static readonly EmojiCase[] EmojiCases =
    [
        new("dna", "DNA — fine curves", "emoji_u1f9ec.png"),
        new("gear", "Gear — hard contrast", "emoji_u2699.png"),
        new("mirror-ball", "Mirror ball — micro detail", "emoji_u1faa9.png"),
        new("chequered-flag", "Chequered flag — flag/detail", "emoji_u1f3c1.png"),
        new("woman-technologist-medium", "Woman technologist — ZWJ + skin", "emoji_u1f469_1f3fd_200d_1f4bb.png"),
        new("family", "Family — long ZWJ", "emoji_u1f468_200d_1f469_200d_1f467_200d_1f466.png"),
        new("butterfly", "Butterfly — curves/detail", "emoji_u1f98b.png"),
        new("bicycle", "Bicycle — thin spokes", "emoji_u1f6b2.png"),
        new("eye-speech-bubble", "Eye in bubble — thin curves", "emoji_u1f441_200d_1f5e8.png")
    ];

    private static readonly DpiCase[] DpiCases =
    [
        new(100, 96.0, 32),
        new(125, 120.0, 40),
        new(150, 144.0, 48),
        new(175, 168.0, 56),
        new(200, 192.0, 64),
        new(225, 216.0, 72),
        new(250, 240.0, 80)
    ];

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string root = ParseRoot(args);
            string results = Path.Combine(root, "results");
            string rendered = Path.Combine(results, "rendered-grid-icons");
            Directory.CreateDirectory(results);
            Directory.CreateDirectory(rendered);

            Dictionary<RenderKey, BitmapSource> renderedIcons = RenderAllGridIcons(root, rendered);
            List<PerformanceMetric> performance = MeasurePerformance(root);
            List<VisualMetric> visual = MeasureVisualDifferences(renderedIcons);

            WriteNativeComparisonSheet(renderedIcons, Path.Combine(results, "comparison-native.png"));
            foreach (DpiCase dpi in DpiCases)
            {
                WriteZoomComparisonSheet(
                    renderedIcons,
                    dpi,
                    Path.Combine(results, $"comparison-zoom4x-{dpi.ScalePercent}.png"));
            }

            WriteMetrics(results, performance, visual);
            Console.WriteLine($"ผลลัพธ์: {results}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static string ParseRoot(string[] args)
    {
        int rootIndex = Array.IndexOf(args, "--root");
        string root = rootIndex >= 0 && rootIndex + 1 < args.Length
            ? args[rootIndex + 1]
            : Directory.GetCurrentDirectory();
        return Path.GetFullPath(root);
    }

    private static Dictionary<RenderKey, BitmapSource> RenderAllGridIcons(string root, string renderedRoot)
    {
        Dictionary<RenderKey, BitmapSource> output = [];

        foreach (DpiCase dpi in DpiCases)
        {
            foreach (int sourceSize in new[] { 128, 512 })
            {
                string outputDirectory = Path.Combine(renderedRoot, dpi.ScalePercent.ToString(CultureInfo.InvariantCulture), sourceSize.ToString(CultureInfo.InvariantCulture));
                Directory.CreateDirectory(outputDirectory);

                foreach (EmojiCase emoji in EmojiCases)
                {
                    string path = AssetPath(root, sourceSize, emoji.FileName);
                    BitmapSource decoded = DecodeForGrid(path, dpi.TargetPixels);
                    BitmapSource gridRender = RenderWpfGridTile(decoded, dpi);
                    RenderKey key = new(sourceSize, dpi.ScalePercent, emoji.Id);
                    output[key] = gridRender;
                    SavePng(gridRender, Path.Combine(outputDirectory, $"{emoji.Id}.png"));
                }
            }
        }

        return output;
    }

    private static List<PerformanceMetric> MeasurePerformance(string root)
    {
        List<PerformanceMetric> metrics = [];

        // Warm up WIC, WPF rendering, JIT, and the filesystem cache outside measurements.
        foreach (int sourceSize in new[] { 128, 512 })
        {
            string warmPath = AssetPath(root, sourceSize, EmojiCases[0].FileName);
            BitmapSource warmDecoded = DecodeForGrid(warmPath, DpiCases[0].TargetPixels);
            _ = RenderWpfGridTile(warmDecoded, DpiCases[0]);
        }

        foreach (DpiCase dpi in DpiCases)
        {
            foreach (int sourceSize in new[] { 128, 512 })
            {
                List<double> decodeMilliseconds = [];
                List<double> renderMilliseconds = [];
                List<long> managedDecodeAllocations = [];

                for (int pass = 0; pass < TimingPasses; pass++)
                {
                    foreach (EmojiCase emoji in EmojiCases)
                    {
                        string path = AssetPath(root, sourceSize, emoji.FileName);
                        long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
                        long decodeStart = Stopwatch.GetTimestamp();
                        BitmapSource decoded = DecodeForGrid(path, dpi.TargetPixels);
                        long decodeEnd = Stopwatch.GetTimestamp();
                        long allocationAfter = GC.GetAllocatedBytesForCurrentThread();

                        long renderStart = Stopwatch.GetTimestamp();
                        _ = RenderWpfGridTile(decoded, dpi);
                        long renderEnd = Stopwatch.GetTimestamp();

                        decodeMilliseconds.Add(Stopwatch.GetElapsedTime(decodeStart, decodeEnd).TotalMilliseconds);
                        renderMilliseconds.Add(Stopwatch.GetElapsedTime(renderStart, renderEnd).TotalMilliseconds);
                        managedDecodeAllocations.Add(allocationAfter - allocationBefore);
                    }
                }

                long encodedBytes = EmojiCases.Sum(emoji => new FileInfo(AssetPath(root, sourceSize, emoji.FileName)).Length);
                long decodedBytesProxy = EmojiCases.Length * (long)dpi.TargetPixels * dpi.TargetPixels * 4;
                metrics.Add(new PerformanceMetric(
                    sourceSize,
                    dpi.ScalePercent,
                    dpi.TargetPixels,
                    Median(decodeMilliseconds),
                    Percentile(decodeMilliseconds, 0.95),
                    Median(renderMilliseconds),
                    Percentile(renderMilliseconds, 0.95),
                    Median(managedDecodeAllocations),
                    encodedBytes,
                    decodedBytesProxy,
                    TimingPasses * EmojiCases.Length));
            }
        }

        return metrics;
    }

    private static List<VisualMetric> MeasureVisualDifferences(Dictionary<RenderKey, BitmapSource> icons)
    {
        List<VisualMetric> metrics = [];

        foreach (DpiCase dpi in DpiCases)
        {
            foreach (EmojiCase emoji in EmojiCases)
            {
                PixelData source128 = ReadPixels(icons[new RenderKey(128, dpi.ScalePercent, emoji.Id)]);
                PixelData source512 = ReadPixels(icons[new RenderKey(512, dpi.ScalePercent, emoji.Id)]);
                DifferenceStats difference = ComparePixels(source128, source512);
                double edge128 = EdgeEnergy(source128);
                double edge512 = EdgeEnergy(source512);
                double edgeDeltaPercent = edge128 == 0.0 ? 0.0 : ((edge512 - edge128) / edge128) * 100.0;

                metrics.Add(new VisualMetric(
                    dpi.ScalePercent,
                    dpi.TargetPixels,
                    emoji.Id,
                    difference.MeanAbsoluteChannelDifference,
                    difference.RootMeanSquareDifference,
                    edge128,
                    edge512,
                    edgeDeltaPercent));
            }
        }

        return metrics;
    }

    private static BitmapSource DecodeForGrid(string path, int targetPixels)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        image.DecodePixelWidth = targetPixels;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static BitmapSource RenderWpfGridTile(BitmapSource source, DpiCase dpi)
    {
        Image image = new()
        {
            Source = source,
            Width = GridDip,
            Height = GridDip,
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        RenderOptions.SetEdgeMode(image, EdgeMode.Unspecified);
        image.Measure(new Size(GridDip, GridDip));
        image.Arrange(new Rect(0, 0, GridDip, GridDip));
        image.UpdateLayout();

        RenderTargetBitmap target = new(dpi.TargetPixels, dpi.TargetPixels, dpi.Dpi, dpi.Dpi, PixelFormats.Pbgra32);
        target.Render(image);
        target.Freeze();
        return target;
    }

    private static void WriteNativeComparisonSheet(Dictionary<RenderKey, BitmapSource> icons, string path)
    {
        const int leftWidth = 210;
        const int groupWidth = 206;
        const int rowHeight = 108;
        const int headerHeight = 132;
        int width = leftWidth + (groupWidth * DpiCases.Length) + 20;
        int height = headerHeight + (rowHeight * EmojiCases.Length) + 30;

        DrawingVisual visual = new();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        using (DrawingContext dc = visual.RenderOpen())
        {
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(245, 247, 250)), null, new Rect(0, 0, width, height));
        DrawText(dc, "WPF 32 DIP grid — native output pixels (1 image px = 1 sheet px)", 20, 16, 18, Brushes.Black);
        DrawText(dc, "BitmapImage.DecodePixelWidth + BitmapScalingMode.HighQuality", 20, 44, 13, Brushes.DimGray);

        for (int dpiIndex = 0; dpiIndex < DpiCases.Length; dpiIndex++)
        {
            DpiCase dpi = DpiCases[dpiIndex];
            double x = leftWidth + (dpiIndex * groupWidth);
            DrawText(dc, $"{dpi.ScalePercent}% · {dpi.TargetPixels}px", x + 25, 70, 14, Brushes.Black);
            DrawText(dc, "128 source", x + 4, 100, 11, Brushes.DarkSlateGray);
            DrawText(dc, "512 source", x + 105, 100, 11, Brushes.DarkSlateGray);
        }

        for (int row = 0; row < EmojiCases.Length; row++)
        {
            EmojiCase emoji = EmojiCases[row];
            double y = headerHeight + (row * rowHeight);
            if (row % 2 == 0)
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(235, 238, 242)), null, new Rect(0, y, width, rowHeight));
            }
            DrawText(dc, emoji.Label, 16, y + 39, 12, Brushes.Black);

            for (int dpiIndex = 0; dpiIndex < DpiCases.Length; dpiIndex++)
            {
                DpiCase dpi = DpiCases[dpiIndex];
                double groupX = leftWidth + (dpiIndex * groupWidth);
                DrawNativeTile(dc, icons[new RenderKey(128, dpi.ScalePercent, emoji.Id)], groupX + 4, y + 10, dpi.TargetPixels);
                DrawNativeTile(dc, icons[new RenderKey(512, dpi.ScalePercent, emoji.Id)], groupX + 105, y + 10, dpi.TargetPixels);
            }
        }
        }

        SaveVisual(visual, width, height, path);
    }

    private static void DrawNativeTile(DrawingContext dc, BitmapSource icon, double x, double y, int pixels)
    {
        const double box = 88;
        dc.DrawRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(205, 210, 218)), 1), new Rect(x, y, box, box));
        double iconX = x + ((box - pixels) / 2.0);
        double iconY = y + ((box - pixels) / 2.0);
        dc.DrawImage(icon, new Rect(iconX, iconY, pixels, pixels));
    }

    private static void WriteZoomComparisonSheet(Dictionary<RenderKey, BitmapSource> icons, DpiCase dpi, string path)
    {
        const int zoom = 4;
        const int leftWidth = 220;
        int tilePixels = dpi.TargetPixels * zoom;
        int columnWidth = tilePixels + 42;
        int rowHeight = tilePixels + 30;
        const int headerHeight = 100;
        int width = leftWidth + (columnWidth * 2) + 20;
        int height = headerHeight + (rowHeight * EmojiCases.Length) + 20;

        DrawingVisual visual = new();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        using (DrawingContext dc = visual.RenderOpen())
        {
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(245, 247, 250)), null, new Rect(0, 0, width, height));
        DrawText(dc, $"32 DIP @ {dpi.ScalePercent}% = {dpi.TargetPixels}px — 4× nearest-neighbor inspection", 18, 14, 18, Brushes.Black);
        DrawText(dc, "128 source", leftWidth + 12, 62, 13, Brushes.DarkSlateGray);
        DrawText(dc, "512 source", leftWidth + columnWidth + 12, 62, 13, Brushes.DarkSlateGray);

        for (int row = 0; row < EmojiCases.Length; row++)
        {
            EmojiCase emoji = EmojiCases[row];
            double y = headerHeight + (row * rowHeight);
            if (row % 2 == 0)
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(235, 238, 242)), null, new Rect(0, y, width, rowHeight));
            }
            DrawText(dc, emoji.Label, 14, y + (tilePixels / 2.0) - 6, 12, Brushes.Black);

            DrawZoomTile(dc, icons[new RenderKey(128, dpi.ScalePercent, emoji.Id)], leftWidth + 10, y + 8, tilePixels);
            DrawZoomTile(dc, icons[new RenderKey(512, dpi.ScalePercent, emoji.Id)], leftWidth + columnWidth + 10, y + 8, tilePixels);
        }
        }

        SaveVisual(visual, width, height, path);
    }

    private static void DrawZoomTile(DrawingContext dc, BitmapSource icon, double x, double y, int tilePixels)
    {
        dc.DrawRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(185, 192, 202)), 1), new Rect(x, y, tilePixels, tilePixels));
        dc.DrawImage(icon, new Rect(x, y, tilePixels, tilePixels));
    }

    private static void DrawText(DrawingContext dc, string text, double x, double y, double fontSize, Brush brush)
    {
        FormattedText formatted = new(
            text,
            CultureInfo.GetCultureInfo("en-US"),
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            fontSize,
            brush,
            1.0);
        dc.DrawText(formatted, new Point(x, y));
    }

    private static void SaveVisual(DrawingVisual visual, int width, int height, string path)
    {
        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        SavePng(bitmap, path);
    }

    private static void SavePng(BitmapSource bitmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    private static PixelData ReadPixels(BitmapSource bitmap)
    {
        BitmapSource converted = bitmap.Format == PixelFormats.Pbgra32
            ? bitmap
            : new FormatConvertedBitmap(bitmap, PixelFormats.Pbgra32, null, 0);
        int stride = converted.PixelWidth * 4;
        byte[] pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        return new PixelData(converted.PixelWidth, converted.PixelHeight, stride, pixels);
    }

    private static DifferenceStats ComparePixels(PixelData first, PixelData second)
    {
        if (first.Width != second.Width || first.Height != second.Height)
        {
            throw new InvalidOperationException("Rendered outputs must have identical dimensions.");
        }

        double absolute = 0;
        double squared = 0;
        for (int index = 0; index < first.Bytes.Length; index++)
        {
            int delta = first.Bytes[index] - second.Bytes[index];
            absolute += Math.Abs(delta);
            squared += delta * delta;
        }

        return new DifferenceStats(
            absolute / first.Bytes.Length,
            Math.Sqrt(squared / first.Bytes.Length));
    }

    private static double EdgeEnergy(PixelData data)
    {
        double[] luminance = new double[data.Width * data.Height];
        for (int y = 0; y < data.Height; y++)
        {
            for (int x = 0; x < data.Width; x++)
            {
                int pixelIndex = (y * data.Stride) + (x * 4);
                double blue = CompositeOnWhite(data.Bytes[pixelIndex], data.Bytes[pixelIndex + 3]);
                double green = CompositeOnWhite(data.Bytes[pixelIndex + 1], data.Bytes[pixelIndex + 3]);
                double red = CompositeOnWhite(data.Bytes[pixelIndex + 2], data.Bytes[pixelIndex + 3]);
                luminance[(y * data.Width) + x] = (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
            }
        }

        double total = 0;
        long samples = 0;
        for (int y = 0; y < data.Height - 1; y++)
        {
            for (int x = 0; x < data.Width - 1; x++)
            {
                double current = luminance[(y * data.Width) + x];
                total += Math.Abs(current - luminance[(y * data.Width) + x + 1]);
                total += Math.Abs(current - luminance[((y + 1) * data.Width) + x]);
                samples += 2;
            }
        }
        return samples == 0 ? 0 : total / samples;
    }

    private static double CompositeOnWhite(byte premultipliedChannel, byte alpha)
        => premultipliedChannel + (255 - alpha);

    private static void WriteMetrics(string results, List<PerformanceMetric> performance, List<VisualMetric> visual)
    {
        JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
        File.WriteAllText(
            Path.Combine(results, "metrics.json"),
            JsonSerializer.Serialize(new { generatedUtc = DateTimeOffset.UtcNow, performance, visual }, jsonOptions));

        StringBuilder performanceCsv = new();
        performanceCsv.AppendLine("sourceSize,dpiPercent,targetPixels,decodeMedianMs,decodeP95Ms,renderMedianMs,renderP95Ms,managedDecodeAllocationMedianBytes,encodedBytesNineAssets,decodedPixelBytesProxyNineTiles,samples");
        foreach (PerformanceMetric metric in performance)
        {
            performanceCsv.AppendLine(string.Join(',',
                metric.SourceSize,
                metric.DpiPercent,
                metric.TargetPixels,
                metric.DecodeMedianMs.ToString("F6", CultureInfo.InvariantCulture),
                metric.DecodeP95Ms.ToString("F6", CultureInfo.InvariantCulture),
                metric.RenderMedianMs.ToString("F6", CultureInfo.InvariantCulture),
                metric.RenderP95Ms.ToString("F6", CultureInfo.InvariantCulture),
                metric.ManagedDecodeAllocationMedianBytes.ToString("F0", CultureInfo.InvariantCulture),
                metric.EncodedBytesNineAssets,
                metric.DecodedPixelBytesProxyNineTiles,
                metric.Samples));
        }
        File.WriteAllText(Path.Combine(results, "performance-metrics.csv"), performanceCsv.ToString());

        StringBuilder visualCsv = new();
        visualCsv.AppendLine("dpiPercent,targetPixels,emoji,meanAbsoluteChannelDifference,rootMeanSquareDifference,edgeEnergy128,edgeEnergy512,edgeEnergy512DeltaPercent");
        foreach (VisualMetric metric in visual)
        {
            visualCsv.AppendLine(string.Join(',',
                metric.DpiPercent,
                metric.TargetPixels,
                metric.Emoji,
                metric.MeanAbsoluteChannelDifference.ToString("F6", CultureInfo.InvariantCulture),
                metric.RootMeanSquareDifference.ToString("F6", CultureInfo.InvariantCulture),
                metric.EdgeEnergy128.ToString("F6", CultureInfo.InvariantCulture),
                metric.EdgeEnergy512.ToString("F6", CultureInfo.InvariantCulture),
                metric.EdgeEnergy512DeltaPercent.ToString("F6", CultureInfo.InvariantCulture)));
        }
        File.WriteAllText(Path.Combine(results, "visual-metrics.csv"), visualCsv.ToString());
    }

    private static string AssetPath(string root, int sourceSize, string fileName)
    {
        string path = Path.Combine(root, "assets", sourceSize.ToString(CultureInfo.InvariantCulture), fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Run fetch-assets.ps1 before the spike.", path);
        }
        return path;
    }

    private static double Median(IReadOnlyCollection<double> values) => Percentile(values, 0.5);

    private static double Median(IReadOnlyCollection<long> values)
    {
        long[] sorted = values.Order().ToArray();
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2.0 : sorted[middle];
    }

    private static double Percentile(IReadOnlyCollection<double> values, double percentile)
    {
        double[] sorted = values.Order().ToArray();
        int index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    private sealed record EmojiCase(string Id, string Label, string FileName);
    private sealed record DpiCase(int ScalePercent, double Dpi, int TargetPixels);
    private readonly record struct RenderKey(int SourceSize, int DpiPercent, string Emoji);
    private sealed record PixelData(int Width, int Height, int Stride, byte[] Bytes);
    private sealed record DifferenceStats(double MeanAbsoluteChannelDifference, double RootMeanSquareDifference);
    private sealed record PerformanceMetric(
        int SourceSize,
        int DpiPercent,
        int TargetPixels,
        double DecodeMedianMs,
        double DecodeP95Ms,
        double RenderMedianMs,
        double RenderP95Ms,
        double ManagedDecodeAllocationMedianBytes,
        long EncodedBytesNineAssets,
        long DecodedPixelBytesProxyNineTiles,
        int Samples);
    private sealed record VisualMetric(
        int DpiPercent,
        int TargetPixels,
        string Emoji,
        double MeanAbsoluteChannelDifference,
        double RootMeanSquareDifference,
        double EdgeEnergy128,
        double EdgeEnergy512,
        double EdgeEnergy512DeltaPercent);
}
