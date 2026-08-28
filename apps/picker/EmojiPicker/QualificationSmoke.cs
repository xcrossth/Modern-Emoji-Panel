using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EmojiPicker;

internal static class QualificationSmoke
{
    internal const double WarmOpenP95BudgetMs = 100;
    internal const double SearchP95BudgetMs = 10;
    internal const double ScrollP95BudgetMs = 60;
    internal const double ScrollMaximumBudgetMs = 150;
    internal const double IdleWorkingSetBudgetMiB = 128;
    internal const double DecodeP95BudgetMs = 15;
    internal const double CacheHitP95BudgetMs = 2;
    internal const int MaximumCachedImages = 256;

    internal static async Task<int> RunAsync(MainWindow window, string reportPath)
    {
        try
        {
            var warmOpen = await window.MeasureWarmOpenToRenderProxyForSmokeAsync(samples: 20);

            var searchSamples = new List<double>(1_000);
            var queries = new[] { "heart", "หัวใจ", "face", "ยิ้ม", "flag", "ธง", "family", "ครอบครัว" };
            for (var index = 0; index < 1_000; index++)
            {
                var stopwatch = Stopwatch.StartNew();
                _ = window.SearchForSmoke(queries[index % queries.Length]);
                stopwatch.Stop();
                searchSamples.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            var scrollFrames = await window.MeasureVirtualizedScrollFramesForSmokeAsync(samples: 100);

            var decodeCandidates = window.SmokeEntries
                .GroupBy(emoji => emoji.AssetPath, StringComparer.Ordinal)
                .Select(group => group.First())
                .Skip(300)
                .Take(128)
                .ToList();
            if (decodeCandidates.Count != 128)
            {
                throw new InvalidOperationException("Not enough distinct grid assets for qualification.");
            }

            var decodeSamples = new List<double>(decodeCandidates.Count);
            foreach (var emoji in decodeCandidates)
            {
                var stopwatch = Stopwatch.StartNew();
                var image = await NotoEmojiAssetProvider.Shared.LoadAsync(emoji.AssetPath, 47);
                stopwatch.Stop();
                if (image == null || !image.IsFrozen)
                {
                    throw new InvalidOperationException($"Grid decode failed for {emoji.Id}.");
                }

                decodeSamples.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            var cacheHitSamples = new List<double>(decodeCandidates.Count);
            foreach (var emoji in decodeCandidates)
            {
                var stopwatch = Stopwatch.StartNew();
                _ = await NotoEmojiAssetProvider.Shared.LoadAsync(emoji.AssetPath, 47);
                stopwatch.Stop();
                cacheHitSamples.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            window.Show();
            await window.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render);
            window.EmojiGrid.SelectedIndex = 0;
            window.EmojiGrid.ScrollIntoView(window.EmojiGrid.SelectedItem);
            window.UpdateLayout();
            await window.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
            var firstContainer = window.EmojiGrid.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
            var accessibleNamePresent = firstContainer != null &&
                !string.IsNullOrWhiteSpace(AutomationProperties.GetName(firstContainer));
            window.Hide();

            MemoryTrimmer.Trim();
            await window.Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle);
            await Task.Delay(100);
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            var idleWorkingSetMiB = process.WorkingSet64 / 1024d / 1024d;

            var warmSummary = MetricSummary.From(warmOpen);
            var searchSummary = MetricSummary.From(searchSamples);
            var scrollSummary = MetricSummary.From(scrollFrames);
            var decodeSummary = MetricSummary.From(decodeSamples);
            var cacheHitSummary = MetricSummary.From(cacheHitSamples);
            var cacheCount = NotoEmojiAssetProvider.Shared.CachedImageCount;

            var checks = new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["warmOpenProxyP95"] = warmSummary.P95Milliseconds <= WarmOpenP95BudgetMs,
                ["searchP95"] = searchSummary.P95Milliseconds <= SearchP95BudgetMs,
                ["scrollP95"] = scrollSummary.P95Milliseconds <= ScrollP95BudgetMs,
                ["scrollMaximum"] = scrollSummary.MaximumMilliseconds <= ScrollMaximumBudgetMs,
                ["idleWorkingSet"] = idleWorkingSetMiB <= IdleWorkingSetBudgetMiB,
                ["gridDecodeP95"] = decodeSummary.P95Milliseconds <= DecodeP95BudgetMs,
                ["cacheHitP95"] = cacheHitSummary.P95Milliseconds <= CacheHitP95BudgetMs,
                ["cacheBound"] = cacheCount <= MaximumCachedImages,
                ["accessibleName"] = accessibleNamePresent,
                ["dpiDecodeWidths"] = new[] { 1d, 1.25d, 1.5d, 1.75d, 2d, 2.25d, 2.5d }
                    .Select(scale => NotoEmojiImage.CalculateDecodePixelWidth(32, scale))
                    .SequenceEqual(new[] { 32, 40, 48, 56, 64, 72, 80 }),
                ["highContrastThemeResolution"] = ThemeManager
                    .ResolveThemeUri(AppThemePreference.Dark, systemDark: true, highContrast: true)
                    .OriginalString.EndsWith("HighContrastTheme.xaml", StringComparison.Ordinal),
            };

            var report = new
            {
                schemaVersion = 1,
                measuredAtUtc = DateTimeOffset.UtcNow,
                runtime = new
                {
                    framework = Environment.Version.ToString(),
                    operatingSystem = Environment.OSVersion.VersionString,
                    processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                },
                modern = new
                {
                    warmOpenToRenderProxy = warmSummary,
                    search = searchSummary,
                    virtualizedScrollFrame = scrollSummary,
                    idleWorkingSetMiB,
                    gridDecode = decodeSummary,
                    cacheHit = cacheHitSummary,
                    cachedImages = cacheCount,
                },
                budgets = new
                {
                    warmOpenP95Milliseconds = WarmOpenP95BudgetMs,
                    searchP95Milliseconds = SearchP95BudgetMs,
                    scrollP95Milliseconds = ScrollP95BudgetMs,
                    scrollMaximumMilliseconds = ScrollMaximumBudgetMs,
                    idleWorkingSetMiB = IdleWorkingSetBudgetMiB,
                    gridDecodeP95Milliseconds = DecodeP95BudgetMs,
                    cacheHitP95Milliseconds = CacheHitP95BudgetMs,
                    maximumCachedImages = MaximumCachedImages,
                },
                checks,
                passed = checks.Values.All(value => value),
                limitations = new[]
                {
                    "warmOpenToRenderProxy bypasses the global keyboard hook, target capture and foreground activation",
                    "scroll timing measures dispatcher render-priority completion; it is not GPU frame telemetry",
                    "idle working set is one post-trim sample from this qualification process",
                },
            };

            var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(
                reportPath,
                JsonSerializer.Serialize(
                    report,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    }));
            return checks.Values.All(value => value) ? 0 : 1;
        }
        catch (Exception exception)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(
                reportPath,
                JsonSerializer.Serialize(
                    new
                    {
                        schemaVersion = 1,
                        measuredAtUtc = DateTimeOffset.UtcNow,
                        passed = false,
                        error = new { type = exception.GetType().Name, exception.Message },
                    },
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    }));
            return 1;
        }
    }

    internal sealed record MetricSummary(
        int Samples,
        double MedianMilliseconds,
        double P95Milliseconds,
        double MaximumMilliseconds)
    {
        internal static MetricSummary From(IEnumerable<double> source)
        {
            var values = source.Order().ToArray();
            if (values.Length == 0)
            {
                throw new ArgumentException("At least one performance sample is required.", nameof(source));
            }

            return new MetricSummary(
                values.Length,
                Math.Round(Percentile(values, 0.50), 4),
                Math.Round(Percentile(values, 0.95), 4),
                Math.Round(values[^1], 4));
        }

        private static double Percentile(IReadOnlyList<double> sorted, double percentile)
        {
            var index = Math.Clamp((int)Math.Ceiling(sorted.Count * percentile) - 1, 0, sorted.Count - 1);
            return sorted[index];
        }
    }
}
