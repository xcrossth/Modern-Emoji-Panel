using System.Text;

namespace EmojiPicker;

internal enum InsertionMethod
{
    UnicodeKeystrokes,
    TemporaryPaste,
}

internal enum TargetValidationFailure
{
    None,
    MissingTarget,
    TargetClosed,
    ForegroundChanged,
    HigherIntegrity,
    IntegrityUnknown,
}

internal sealed record InsertionResult(
    bool Accepted,
    InsertionMethod? Method,
    string? Message,
    TargetValidationFailure TargetFailure = TargetValidationFailure.None,
    uint AcceptedInputCount = 0,
    uint RequestedInputCount = 0)
{
    public static InsertionResult Failure(string message, TargetValidationFailure targetFailure = TargetValidationFailure.None) =>
        new(false, null, message, targetFailure);
}

internal static class InsertionPolicy
{
    public static InsertionMethod SelectMethod(EmojiInsertMode mode, string sequence) => mode switch
    {
        EmojiInsertMode.Paste => InsertionMethod.TemporaryPaste,
        EmojiInsertMode.Keystroke => InsertionMethod.UnicodeKeystrokes,
        _ when IsComplexSequence(sequence) => InsertionMethod.TemporaryPaste,
        _ => InsertionMethod.UnicodeKeystrokes,
    };

    public static bool IsComplexSequence(string sequence)
    {
        var scalarCount = 0;
        var containsComplexMarker = false;
        foreach (var rune in sequence.EnumerateRunes())
        {
            if (rune.Value == 0xFE0F)
            {
                continue;
            }

            scalarCount++;
            containsComplexMarker |= rune.Value is 0x200D or 0x20E3 ||
                rune.Value is >= 0x1F1E6 and <= 0x1F1FF ||
                rune.Value is >= 0x1F3FB and <= 0x1F3FF;
        }

        return scalarCount > 1 || containsComplexMarker;
    }
}

internal static class TargetValidationPolicy
{
    public static TargetValidationFailure Validate(
        nint capturedTarget,
        bool targetExists,
        nint foregroundTarget,
        int? currentIntegrityRid,
        int? targetIntegrityRid)
    {
        if (capturedTarget == nint.Zero)
        {
            return TargetValidationFailure.MissingTarget;
        }

        if (!targetExists)
        {
            return TargetValidationFailure.TargetClosed;
        }

        if (foregroundTarget != capturedTarget)
        {
            return TargetValidationFailure.ForegroundChanged;
        }

        if (!currentIntegrityRid.HasValue || !targetIntegrityRid.HasValue)
        {
            return TargetValidationFailure.IntegrityUnknown;
        }

        return targetIntegrityRid.Value > currentIntegrityRid.Value
            ? TargetValidationFailure.HigherIntegrity
            : TargetValidationFailure.None;
    }
}

internal static class ClipboardRestorePolicy
{
    public static bool ShouldRestore(bool snapshotAvailable, uint sequenceAfterTemporarySet, uint currentSequence) =>
        snapshotAvailable && sequenceAfterTemporarySet == currentSequence;
}
