using System.Globalization;
using System.Text;

namespace EmojiPicker;

internal enum QueueEnqueueStatus
{
    Accepted,
    Full,
    Stopped,
}

internal readonly record struct QueueEnqueueResult(
    QueueEnqueueStatus Status,
    int PendingCount,
    int Capacity);

internal enum QueueTerminalKind
{
    CommitDismiss,
    Dismiss,
    TypingHandoff,
}

internal enum TypingHandoffKind
{
    CommittedText,
    Shortcut,
}

[Flags]
internal enum ShortcutModifiers
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
    Windows = 8,
}

internal sealed record TypingHandoffPayload(
    TypingHandoffKind Kind,
    string? CommittedText = null,
    ushort VirtualKey = 0,
    ShortcutModifiers Modifiers = ShortcutModifiers.None)
{
    internal static TypingHandoffPayload Text(string committedText) =>
        new(TypingHandoffKind.CommittedText, CommittedText: committedText);

    internal static TypingHandoffPayload Shortcut(ushort virtualKey, ShortcutModifiers modifiers) =>
        new(TypingHandoffKind.Shortcut, VirtualKey: virtualKey, Modifiers: modifiers);
}

/// <summary>
/// Describes what must happen after the active insertion finishes. A committed
/// Typing Handoff payload lives only in this in-memory object: it is never logged,
/// persisted or copied to the clipboard without a separate explicit action.
/// </summary>
internal sealed record QueueTerminalIntent(
    QueueTerminalKind Kind,
    bool ReturnFocusToTarget,
    TypingHandoffPayload? Handoff = null)
{
    internal static QueueTerminalIntent AfterCommit() =>
        new(QueueTerminalKind.CommitDismiss, ReturnFocusToTarget: false);

    internal static QueueTerminalIntent Dismiss(PickerDismissReason reason) =>
        new(QueueTerminalKind.Dismiss, PickerSessionState.ReturnsFocusAfter(reason));

    internal static QueueTerminalIntent TypingHandoff(string committedText) =>
        TypingHandoff(TypingHandoffPayload.Text(committedText));

    internal static QueueTerminalIntent TypingHandoff(TypingHandoffPayload payload) =>
        new(QueueTerminalKind.TypingHandoff, ReturnFocusToTarget: true, payload);
}

/// <summary>
/// Dispatcher-confined bounded FIFO. Enqueue only creates pending work;
/// TryStartNext is the single seam that promotes one item to Active, preventing
/// parallel insertion without relying on desktop timing or locks.
/// </summary>
internal sealed class InsertionQueue<T>
    where T : class
{
    private readonly Queue<T> pending = new();

    internal InsertionQueue(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        Capacity = capacity;
    }

    internal int Capacity { get; }

    internal int PendingCount => pending.Count;

    internal T? Active { get; private set; }

    internal bool IsAccepting { get; private set; } = true;

    internal bool IsFull => IsAccepting && pending.Count >= Capacity;

    internal bool HasWork => Active != null || pending.Count > 0;

    internal QueueTerminalIntent? TerminalIntent { get; private set; }

    internal bool IsTerminalReady => TerminalIntent != null && !HasWork;

    internal QueueEnqueueResult Enqueue(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!IsAccepting)
        {
            return new QueueEnqueueResult(QueueEnqueueStatus.Stopped, pending.Count, Capacity);
        }

        if (pending.Count >= Capacity)
        {
            return new QueueEnqueueResult(QueueEnqueueStatus.Full, pending.Count, Capacity);
        }

        pending.Enqueue(item);
        return new QueueEnqueueResult(QueueEnqueueStatus.Accepted, pending.Count, Capacity);
    }

    internal bool TryStartNext(out T? item)
    {
        if (Active != null || pending.Count == 0)
        {
            item = null;
            return false;
        }

        Active = pending.Dequeue();
        item = Active;
        return true;
    }

    internal void CompleteActive()
    {
        if (Active == null)
        {
            throw new InvalidOperationException("No active insertion can be completed.");
        }

        Active = null;
    }

    /// <summary>
    /// Stops new work and cancels every item that has not crossed the Active seam.
    /// Active is deliberately left alone and must be completed by its adapter.
    /// </summary>
    internal int StopAndCancelPending(QueueTerminalIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        IsAccepting = false;
        TerminalIntent = intent;
        var cancelled = pending.Count;
        pending.Clear();
        return cancelled;
    }

    internal int CancelPendingAndStop()
    {
        IsAccepting = false;
        var cancelled = pending.Count;
        pending.Clear();
        return cancelled;
    }

    /// <summary>
    /// Stops new work but preserves FIFO items already accepted. Used by Enter:
    /// its insertion and every earlier selection drain before the session closes.
    /// </summary>
    internal void StopAfterDrain(QueueTerminalIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        IsAccepting = false;
        TerminalIntent = intent;
    }

    internal void Reset()
    {
        if (HasWork)
        {
            throw new InvalidOperationException("The insertion queue cannot reset while work remains.");
        }

        TerminalIntent = null;
        IsAccepting = true;
    }
}

internal static class TypingHandoffInput
{
    private const ushort VkShift = 0x10;
    private const ushort VkControl = 0x11;
    private const ushort VkAlt = 0x12;
    private const ushort VkLeftWindows = 0x5B;
    private const ushort VkRightWindows = 0x5C;

    /// <summary>
    /// Accepts only text produced by WPF's committed TextInput event. IME pre-edit,
    /// dead-key prefixes and shortcuts do not reach this seam as committed printable
    /// text; control characters are rejected as an additional shortcut guard.
    /// </summary>
    internal static bool TryCaptureCommittedText(string? text, out string committedText)
    {
        committedText = string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var hasPrintableRune = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.Control)
            {
                return false;
            }

            hasPrintableRune = true;
        }

        if (!hasPrintableRune)
        {
            return false;
        }

        committedText = text;
        return true;
    }

    /// <summary>
    /// Captures a complete shortcut chord rather than treating its key as text.
    /// Shift alone is excluded because its printable result arrives through the
    /// committed TextInput path. Modifier-only events are also excluded.
    /// </summary>
    internal static bool TryCaptureShortcut(
        int virtualKey,
        ShortcutModifiers modifiers,
        out TypingHandoffPayload payload)
    {
        payload = TypingHandoffPayload.Shortcut(0, ShortcutModifiers.None);
        if (virtualKey is <= 0 or > byte.MaxValue ||
            (modifiers & (ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Windows)) == 0 ||
            virtualKey is VkShift or VkControl or VkAlt or VkLeftWindows or VkRightWindows)
        {
            return false;
        }

        payload = TypingHandoffPayload.Shortcut((ushort)virtualKey, modifiers);
        return true;
    }
}
