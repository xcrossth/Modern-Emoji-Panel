using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace EmojiPicker;

internal static class AtomicSupplementaryTextTargetPolicy
{
    // Chromium page editors and browser chrome can turn the two UTF-16 units of a
    // supplementary scalar into U+FFFD after the Picker focus round-trip. Treat
    // Chrome accessibility text-edit controls as atomic-text targets;
    // InsertionPolicy still limits Temporary Paste to supplementary sequences
    // and preserves the explicit Keystroke-only override.
    internal static bool RequiresAtomicText(string frameworkId, ControlType controlType) =>
        string.Equals(frameworkId, "Chrome", StringComparison.OrdinalIgnoreCase) &&
        controlType == ControlType.Edit;
}

/// <summary>
/// Keeps the exact accessibility element that owned text focus before the Picker
/// activated. Chromium and Explorer chrome render address/search editors without
/// a child HWND, so the native focus HWND alone cannot reopen their editable state.
/// </summary>
internal sealed class AccessibilityFocusSnapshot
{
    private readonly AutomationElement element;
    private readonly string frameworkId;
    private readonly ControlType controlType;

    private AccessibilityFocusSnapshot(
        AutomationElement element,
        string frameworkId,
        ControlType controlType)
    {
        this.element = element;
        this.frameworkId = frameworkId;
        this.controlType = controlType;
    }

    internal bool RequiresAtomicSupplementaryText =>
        AtomicSupplementaryTextTargetPolicy.RequiresAtomicText(frameworkId, controlType);

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
                    current.FrameworkId,
                    current.ControlType)
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
