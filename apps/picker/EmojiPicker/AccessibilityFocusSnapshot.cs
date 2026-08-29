using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace EmojiPicker;

/// <summary>
/// Keeps the exact accessibility element that owned text focus before the Picker
/// activated. Chromium and Explorer chrome render address/search editors without
/// a child HWND, so the native focus HWND alone cannot reopen their editable state.
/// </summary>
internal sealed class AccessibilityFocusSnapshot
{
    private readonly AutomationElement element;
    private readonly string className;
    private readonly string frameworkId;

    private AccessibilityFocusSnapshot(
        AutomationElement element,
        string className,
        string frameworkId)
    {
        this.element = element;
        this.className = className;
        this.frameworkId = frameworkId;
    }

    internal bool RequiresAtomicSupplementaryText =>
        string.Equals(frameworkId, "Chrome", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(className, "OmniboxViewViews", StringComparison.Ordinal);

    internal static AccessibilityFocusSnapshot? Capture(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero || !NativeMethods.IsWindow(targetWindow))
        {
            return null;
        }

        try
        {
            if (NativeMethods.GetForegroundWindow() != targetWindow ||
                !NativeMethods.GetWindowRect(targetWindow, out var targetRect))
            {
                return null;
            }

            NativeMethods.GetWindowThreadProcessId(targetWindow, out var targetProcessId);
            var focused = AutomationElement.FocusedElement;
            if (focused == null)
            {
                return null;
            }

            var current = focused.Current;
            var bounds = current.BoundingRectangle;
            var belongsToTarget = current.ProcessId == targetProcessId ||
                (bounds.Width > 0 && bounds.Height > 0 &&
                 bounds.Right > targetRect.Left && bounds.Left < targetRect.Right &&
                 bounds.Bottom > targetRect.Top && bounds.Top < targetRect.Bottom);
            return belongsToTarget
                ? new AccessibilityFocusSnapshot(
                    focused,
                    current.ClassName,
                    current.FrameworkId)
                : null;
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    internal bool TryRestore()
    {
        try
        {
            element.SetFocus();
            return true;
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
        catch (ElementNotEnabledException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (COMException)
        {
            return false;
        }
    }
}
