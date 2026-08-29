using System;
using System.Drawing;

namespace EmojiPicker;

internal enum PickerInputMode
{
    Browse,
    Search,
}

internal enum CommitGesture
{
    Pointer,
    Enter,
    ShiftEnter,
}

internal enum PickerDismissReason
{
    Escape,
    CloseButton,
    ExternalPointer,
    TypingHandoff,
    ProcessExit,
}

internal readonly record struct EscapeOutcome(bool Dismiss, bool ReturnFocusToTarget);

internal sealed class PickerSessionState
{
    public PickerInputMode Mode { get; private set; } = PickerInputMode.Browse;

    public void Begin() => Mode = PickerInputMode.Browse;

    public void EnterSearch() => Mode = PickerInputMode.Search;

    public void EnterBrowse() => Mode = PickerInputMode.Browse;

    public EscapeOutcome Escape()
    {
        if (Mode == PickerInputMode.Search)
        {
            Mode = PickerInputMode.Browse;
            return new EscapeOutcome(Dismiss: false, ReturnFocusToTarget: false);
        }

        return new EscapeOutcome(Dismiss: true, ReturnFocusToTarget: true);
    }

    public static bool ContinuesAfter(CommitGesture gesture) => gesture != CommitGesture.Enter;

    public static bool ShouldHideDuringInsertion(CommitGesture gesture) => !ContinuesAfter(gesture);

    public static bool ReturnsFocusAfter(PickerDismissReason reason) => reason != PickerDismissReason.ExternalPointer;
}

internal readonly record struct PickerPlacementResult(int Left, int Top, string Anchor);

internal static class PickerPlacement
{
    internal static PickerPlacementResult Calculate(
        Rectangle? caret,
        Rectangle? target,
        Rectangle workingArea,
        int pickerWidth,
        int pickerHeight,
        int gap = 8)
    {
        var width = Math.Min(Math.Max(1, pickerWidth), workingArea.Width);
        var height = Math.Min(Math.Max(1, pickerHeight), workingArea.Height);

        int anchorX;
        int anchorTop;
        int anchorBottom;
        string anchor;
        if (caret is Rectangle caretRect)
        {
            anchorX = caretRect.Left;
            anchorTop = caretRect.Top;
            anchorBottom = caretRect.Bottom;
            anchor = "caret";
        }
        else if (target is Rectangle targetRect)
        {
            anchorX = targetRect.Left + (targetRect.Width / 2) - (width / 2);
            anchorTop = targetRect.Top + (targetRect.Height / 2);
            anchorBottom = anchorTop;
            anchor = "target-center";
        }
        else
        {
            anchorX = workingArea.Left + ((workingArea.Width - width) / 2);
            anchorTop = workingArea.Top + ((workingArea.Height - height) / 2);
            anchorBottom = anchorTop;
            anchor = "working-area-center";
        }

        var leftCandidate = anchor == "caret" ? anchorX + gap : anchorX;
        var left = Math.Clamp(leftCandidate, workingArea.Left, workingArea.Right - width);

        int topCandidate;
        if (anchor == "caret" && anchorBottom + gap + height <= workingArea.Bottom)
        {
            topCandidate = anchorBottom + gap;
        }
        else if (anchor == "caret" && anchorTop - gap - height >= workingArea.Top)
        {
            topCandidate = anchorTop - gap - height;
        }
        else if (anchor == "target-center")
        {
            topCandidate = anchorTop - (height / 2);
        }
        else if (anchor == "working-area-center")
        {
            topCandidate = anchorTop;
        }
        else
        {
            topCandidate = anchorBottom + gap;
        }

        var top = Math.Clamp(topCandidate, workingArea.Top, workingArea.Bottom - height);
        return new PickerPlacementResult(left, top, anchor);
    }
}
