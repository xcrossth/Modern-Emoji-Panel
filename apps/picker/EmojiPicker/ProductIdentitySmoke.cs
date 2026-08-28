using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;

namespace EmojiPicker
{
    internal static class ProductIdentitySmoke
    {
        public static int Run(string reportPath)
        {
            var modernValues = new[]
            {
                ProductIdentity.ExecutableBaseName,
                ProductIdentity.MutexName,
                ProductIdentity.ShowEventName,
                ProductIdentity.RunValueName,
                ProductIdentity.DataDirectoryName,
            };

            var identityIsIsolated = Array.TrueForAll(
                modernValues,
                value => !value.Contains("ClassicEmojiPicker", StringComparison.OrdinalIgnoreCase));
            identityIsIsolated &= !string.Equals(
                ProductIdentity.DataDirectory,
                ClassicProductIdentity.DataDirectory,
                StringComparison.OrdinalIgnoreCase);

            var conflictPositive = new ClassicConflictDetector(name =>
                name == ClassicProductIdentity.MutexName).IsClassicRunning();
            var conflictNegative = !new ClassicConflictDetector(_ => false).IsClassicRunning();
            var namedMutexProbe = VerifyNamedMutexProbe();
            var singleInstanceSignal = VerifySingleInstanceSignal();
            var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            var assemblyMatches = string.Equals(
                assemblyName,
                ProductIdentity.ExecutableBaseName,
                StringComparison.Ordinal);

            var passed = identityIsIsolated && conflictPositive && conflictNegative && namedMutexProbe &&
                singleInstanceSignal && assemblyMatches;
            var report = new
            {
                generatedAtUtc = DateTime.UtcNow,
                passed,
                productName = ProductIdentity.ProductName,
                executableName = ProductIdentity.ExecutableName,
                assemblyName,
                mutexName = ProductIdentity.MutexName,
                showEventName = ProductIdentity.ShowEventName,
                runValueName = ProductIdentity.RunValueName,
                dataDirectory = ProductIdentity.DataDirectory,
                classicDataDirectory = ClassicProductIdentity.DataDirectory,
                identityIsIsolated,
                conflictPositive,
                conflictNegative,
                namedMutexProbe,
                singleInstanceSignal,
                assemblyMatches,
            };

            var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                reportPath,
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            return passed ? 0 : 1;
        }

        private static bool VerifyNamedMutexProbe()
        {
            var name = $"Local\\XCroSs.ModernEmojiPicker.Smoke.Probe.{Guid.NewGuid():N}";
            using var mutex = new Mutex(true, name, out var createdNew);
            return createdNew && ClassicConflictDetector.NamedMutexExists(name);
        }

        private static bool VerifySingleInstanceSignal()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var mutexName = $"Local\\XCroSs.ModernEmojiPicker.Smoke.{suffix}";
            var eventName = $"Local\\XCroSs.ModernEmojiPicker.Smoke.Show.{suffix}";

            if (!SingleInstanceCoordinator.TryAcquire(mutexName, eventName, out var primary) || primary == null)
            {
                return false;
            }

            using (primary)
            using (var signaled = new ManualResetEventSlim())
            {
                primary.ShowRequested += signaled.Set;
                primary.StartListening();

                var becameSecond = !SingleInstanceCoordinator.TryAcquire(
                    mutexName,
                    eventName,
                    out var unexpectedPrimary);
                unexpectedPrimary?.Dispose();
                return becameSecond && signaled.Wait(TimeSpan.FromSeconds(2));
            }
        }
    }
}
