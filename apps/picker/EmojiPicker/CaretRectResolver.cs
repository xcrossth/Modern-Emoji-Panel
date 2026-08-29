using System;
using System.Drawing;

namespace EmojiPicker;

internal delegate bool CaretRectCapture(IntPtr topLevel, out Rectangle rect);

internal static class CaretRectResolver
{
    internal static bool TryResolve(
        IntPtr topLevel,
        CaretRectCapture nativeCapture,
        CaretRectCapture accessibilityCapture,
        out Rectangle rect)
    {
        if (nativeCapture(topLevel, out rect) && IsUsable(rect))
        {
            return true;
        }

        if (accessibilityCapture(topLevel, out rect) && IsUsable(rect))
        {
            return true;
        }

        rect = default;
        return false;
    }

    private static bool IsUsable(Rectangle rect) => rect.Width > 0 && rect.Height > 0;
}
