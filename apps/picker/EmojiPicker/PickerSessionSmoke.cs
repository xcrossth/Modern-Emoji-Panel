using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace EmojiPicker;

internal static class PickerSessionSmoke
{
    internal static int Run(string reportPath)
    {
        var checks = new Dictionary<string, bool>(StringComparer.Ordinal);
        var session = new PickerSessionState();
        session.Begin();
        checks["opens-in-browse-mode"] = session.Mode == PickerInputMode.Browse;

        session.EnterSearch();
        var firstEscape = session.Escape();
        checks["first-search-escape-returns-to-browse"] =
            !firstEscape.Dismiss && session.Mode == PickerInputMode.Browse;
        var secondEscape = session.Escape();
        checks["second-escape-dismisses-and-restores-target"] =
            secondEscape.Dismiss && secondEscape.ReturnFocusToTarget;

        checks["pointer-continues-session"] = PickerSessionState.ContinuesAfter(CommitGesture.Pointer);
        checks["enter-dismisses-session"] = !PickerSessionState.ContinuesAfter(CommitGesture.Enter);
        checks["shift-enter-continues-session"] = PickerSessionState.ContinuesAfter(CommitGesture.ShiftEnter);
        checks["pointer-insertion-keeps-picker-visible"] =
            !PickerSessionState.ShouldHideDuringInsertion(CommitGesture.Pointer);
        checks["dismiss-commit-may-hide-picker"] =
            PickerSessionState.ShouldHideDuringInsertion(CommitGesture.Enter);
        checks["outside-click-preserves-user-focus"] =
            !PickerSessionState.ReturnsFocusAfter(PickerDismissReason.ExternalPointer);
        checks["explicit-dismiss-restores-target"] =
            PickerSessionState.ReturnsFocusAfter(PickerDismissReason.Escape) &&
            PickerSessionState.ReturnsFocusAfter(PickerDismissReason.CloseButton);

        var primaryArea = new Rectangle(0, 0, 1920, 1040);
        var belowCaret = PickerPlacement.Calculate(
            new Rectangle(1800, 100, 2, 24),
            new Rectangle(100, 100, 1600, 800),
            primaryArea,
            400,
            440);
        checks["caret-placement-clamps-to-working-area"] =
            belowCaret.Anchor == "caret" && belowCaret.Left == 1520 && belowCaret.Top == 132;

        var aboveCaret = PickerPlacement.Calculate(
            new Rectangle(500, 1000, 2, 24),
            null,
            primaryArea,
            400,
            440);
        checks["caret-placement-flips-above"] = aboveCaret.Top == 552;

        var secondaryArea = new Rectangle(-1600, 0, 1600, 860);
        var targetFallback = PickerPlacement.Calculate(
            null,
            new Rectangle(-1500, 100, 1200, 700),
            secondaryArea,
            400,
            440);
        checks["target-fallback-stays-on-target-monitor"] =
            targetFallback.Anchor == "target-center" &&
            targetFallback.Left >= secondaryArea.Left &&
            targetFallback.Left + 400 <= secondaryArea.Right &&
            targetFallback.Top >= secondaryArea.Top &&
            targetFallback.Top + 440 <= secondaryArea.Bottom;

        var accessibilityCaret = new Rectangle(24, 48, 2, 22);
        checks["accessibility-caret-follows-missing-native-caret"] =
            CaretRectResolver.TryResolve(
                new IntPtr(42),
                static (IntPtr _, out Rectangle rect) =>
                {
                    rect = default;
                    return false;
                },
                (IntPtr _, out Rectangle rect) =>
                {
                    rect = accessibilityCaret;
                    return true;
                },
                out var resolvedCaret) &&
            resolvedCaret == accessibilityCaret;

        var settingsRoot = Path.Combine(Path.GetTempPath(), $"modern-emoji-picker-session-{Guid.NewGuid():N}");
        var settingsPath = Path.Combine(settingsRoot, "settings.json");
        try
        {
            var settings = new Settings { PickerWidth = 612.5, PickerHeight = 578.25 };
            settings.SaveTo(settingsPath);
            var reloaded = Settings.LoadFrom(settingsPath);
            checks["window-size-persists"] =
                Math.Abs(reloaded.PickerWidth - 612.5) < 0.01 &&
                Math.Abs(reloaded.PickerHeight - 578.25) < 0.01;
        }
        finally
        {
            if (Directory.Exists(settingsRoot))
            {
                Directory.Delete(settingsRoot, recursive: true);
            }
        }

        var passed = checks.Values.All(value => value);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllText(
            reportPath,
            JsonSerializer.Serialize(new { passed, checks }, new JsonSerializerOptions { WriteIndented = true }));
        return passed ? 0 : 1;
    }
}
