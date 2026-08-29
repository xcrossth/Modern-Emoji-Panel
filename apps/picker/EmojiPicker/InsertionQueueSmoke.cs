using System.IO;
using System.Text.Json;

namespace EmojiPicker;

internal static class InsertionQueueSmoke
{
    internal static int Run(string reportPath)
    {
        var checks = new Dictionary<string, bool>(StringComparer.Ordinal);

        var ordered = new InsertionQueue<string>(capacity: 20);
        checks["first-work-enters-pending-state"] =
            ordered.Enqueue("01").Status == QueueEnqueueStatus.Accepted && ordered.PendingCount == 1;
        checks["single-active-seam-starts-first-item"] =
            ordered.TryStartNext(out var first) && first == "01" && ordered.Active == "01";
        checks["parallel-start-is-refused"] = !ordered.TryStartNext(out _);

        for (var index = 2; index <= 21; index++)
        {
            var result = ordered.Enqueue(index.ToString("00"));
            if (result.Status != QueueEnqueueStatus.Accepted)
            {
                checks["twenty-waiting-items-are-accepted"] = false;
                break;
            }
        }

        checks.TryAdd("twenty-waiting-items-are-accepted", ordered.PendingCount == 20);
        var full = ordered.Enqueue("22");
        checks["twenty-first-waiting-item-is-reported-full"] =
            full.Status == QueueEnqueueStatus.Full && full.PendingCount == 20 && ordered.IsFull;

        var insertionOrder = new List<string> { first! };
        ordered.CompleteActive();
        while (ordered.TryStartNext(out var next))
        {
            insertionOrder.Add(next!);
            ordered.CompleteActive();
        }

        checks["click-order-equals-insertion-order"] =
            insertionOrder.SequenceEqual(Enumerable.Range(1, 21).Select(index => index.ToString("00")));
        ordered.Reset();

        var cancelled = new InsertionQueue<string>(capacity: 20);
        cancelled.Enqueue("active");
        cancelled.TryStartNext(out _);
        cancelled.Enqueue("pending-1");
        cancelled.Enqueue("pending-2");
        var thaiInput = "ก้";
        var cancelledCount = cancelled.StopAndCancelPending(QueueTerminalIntent.TypingHandoff(thaiInput));
        checks["dismiss-cancels-only-not-started-work"] =
            cancelledCount == 2 && cancelled.Active == "active" && cancelled.PendingCount == 0;
        checks["active-operation-must-finish-before-handoff"] = !cancelled.IsTerminalReady;
        checks["new-work-is-rejected-after-handoff-starts"] =
            cancelled.Enqueue("too-late").Status == QueueEnqueueStatus.Stopped;
        cancelled.CompleteActive();
        checks["committed-thai-input-survives-active-operation"] =
            cancelled.IsTerminalReady &&
            cancelled.TerminalIntent?.Handoff?.CommittedText == thaiInput &&
            cancelled.TerminalIntent.ReturnFocusToTarget;
        cancelled.Reset();

        var draining = new InsertionQueue<string>(capacity: 20);
        draining.Enqueue("pointer");
        draining.Enqueue("enter");
        draining.StopAfterDrain(QueueTerminalIntent.AfterCommit());
        var drainedOrder = new List<string>();
        while (draining.TryStartNext(out var next))
        {
            drainedOrder.Add(next!);
            draining.CompleteActive();
        }

        checks["enter-drains-earlier-accepted-work-in-order"] =
            drainedOrder.SequenceEqual(new[] { "pointer", "enter" }) && draining.IsTerminalReady;
        checks["enter-stops-later-work"] =
            draining.Enqueue("after-enter").Status == QueueEnqueueStatus.Stopped;

        var external = new InsertionQueue<string>(capacity: 20);
        external.Enqueue("not-started");
        external.StopAndCancelPending(QueueTerminalIntent.Dismiss(PickerDismissReason.ExternalPointer));
        checks["outside-dismiss-is-ready-without-focus-steal"] =
            external.IsTerminalReady && external.TerminalIntent?.ReturnFocusToTarget == false;

        var explicitDismiss = new InsertionQueue<string>(capacity: 20);
        explicitDismiss.Enqueue("active");
        explicitDismiss.TryStartNext(out _);
        explicitDismiss.StopAndCancelPending(QueueTerminalIntent.Dismiss(PickerDismissReason.Escape));
        var waitsBeforeFocusRestore = !explicitDismiss.IsTerminalReady;
        explicitDismiss.CompleteActive();
        checks["explicit-dismiss-restores-focus-only-after-active-finishes"] =
            waitsBeforeFocusRestore &&
            explicitDismiss.IsTerminalReady &&
            explicitDismiss.TerminalIntent?.ReturnFocusToTarget == true;

        checks["thai-ime-commit-is-printable"] =
            TypingHandoffInput.TryCaptureCommittedText("เก่ง", out var thai) && thai == "เก่ง";
        checks["dead-key-result-is-forwarded-once"] =
            TypingHandoffInput.TryCaptureCommittedText("é", out var composed) && composed == "é";
        checks["surrogate-pair-is-preserved"] =
            TypingHandoffInput.TryCaptureCommittedText("😀", out var emoji) && emoji == "😀";
        checks["shortcut-control-input-is-not-captured"] =
            !TypingHandoffInput.TryCaptureCommittedText("\u0003", out _);
        checks["ime-preedit-without-commit-is-not-captured"] =
            !TypingHandoffInput.TryCaptureCommittedText(string.Empty, out _);
        checks["control-shortcut-is-captured-as-a-chord"] =
            TypingHandoffInput.TryCaptureShortcut(
                0x43,
                ShortcutModifiers.Control,
                out var copyShortcut) &&
            copyShortcut == TypingHandoffPayload.Shortcut(0x43, ShortcutModifiers.Control);
        checks["alt-shift-shortcut-preserves-all-modifiers"] =
            TypingHandoffInput.TryCaptureShortcut(
                0x53,
                ShortcutModifiers.Alt | ShortcutModifiers.Shift,
                out var altShiftShortcut) &&
            altShiftShortcut.Modifiers == (ShortcutModifiers.Alt | ShortcutModifiers.Shift);
        checks["shift-only-printable-key-waits-for-committed-text"] =
            !TypingHandoffInput.TryCaptureShortcut(0x41, ShortcutModifiers.Shift, out _);
        checks["modifier-only-key-is-not-a-shortcut"] =
            !TypingHandoffInput.TryCaptureShortcut(0x11, ShortcutModifiers.Control, out _);

        var passed = checks.Values.All(value => value);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllText(
            reportPath,
            JsonSerializer.Serialize(new { passed, checks }, new JsonSerializerOptions { WriteIndented = true }));
        return passed ? 0 : 1;
    }
}
