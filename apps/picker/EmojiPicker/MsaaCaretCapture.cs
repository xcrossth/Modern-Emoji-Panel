using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace EmojiPicker;

/// <summary>
/// Reads the process-wide caret exposed through Microsoft Active Accessibility.
/// Chromium/Electron rendered text controls commonly expose OBJID_CARET even when
/// they do not create a Win32 hwndCaret for GetGUIThreadInfo.
/// </summary>
internal static class MsaaCaretCapture
{
    private const uint ObjIdCaret = 0xFFFFFFF8;
    private const int ChildIdSelf = 0;

    internal static bool TryGetCaretRect(IntPtr topLevel, out Rectangle rect)
    {
        rect = default;
        if (topLevel == IntPtr.Zero ||
            NativeMethods.GetForegroundWindow() != topLevel ||
            !NativeMethods.GetWindowRect(topLevel, out var targetRect))
        {
            return false;
        }

        var focusedControl = TextInjector.GetFocusedControl(topLevel);
        foreach (var sourceWindow in new[] { focusedControl, IntPtr.Zero })
        {
            object accessibleObject = null!;
            try
            {
                var accessibleId = typeof(Accessibility.IAccessible).GUID;
                var result = NativeMethods.AccessibleObjectFromWindow(
                    sourceWindow,
                    ObjIdCaret,
                    ref accessibleId,
                    ref accessibleObject);
                if (result < 0 || accessibleObject is not Accessibility.IAccessible accessible)
                {
                    continue;
                }

                accessible.accLocation(out var left, out var top, out var width, out var height, ChildIdSelf);
                if (width <= 0 || height <= 0)
                {
                    continue;
                }

                var candidate = new Rectangle(left, top, width, height);
                if (!Intersects(targetRect, candidate))
                {
                    continue;
                }

                rect = candidate;
                return true;
            }
            catch (COMException ex)
            {
                Logger.Log($"MSAA caret unavailable ({ex.GetType().Name})");
            }
            catch (ArgumentException ex)
            {
                Logger.Log($"MSAA caret location invalid ({ex.GetType().Name})");
            }
            finally
            {
                if (accessibleObject != null && Marshal.IsComObject(accessibleObject))
                {
                    Marshal.ReleaseComObject(accessibleObject);
                }
            }
        }

        return false;
    }

    private static bool Intersects(NativeMethods.RECT target, Rectangle candidate) =>
        candidate.Right > target.Left && candidate.Left < target.Right &&
        candidate.Bottom > target.Top && candidate.Top < target.Bottom;
}
