using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Widglass;

/// <summary>
/// Pins a widget window to the desktop, like a macOS widget:
/// - it belongs to the Windows desktop window (the one that holds the desktop icons),
///   so it stays visible on "Show desktop" (Win+D) and is never minimized;
/// - it always stays at the bottom, behind every normal window, even when clicked;
/// - it doesn't appear in the taskbar or in Alt+Tab.
/// It can still be dragged and clicked (e.g. the music buttons).
/// </summary>
public static class DesktopPin
{
    private static readonly int TaskbarCreated = RegisterWindowMessage("TaskbarCreated");

    public static void Attach(Window window) =>
        window.SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            Pin(hwnd);
            HwndSource.FromHwnd(hwnd)?.AddHook(Hook);
        };

    private static void Pin(IntPtr hwnd)
    {
        // Tool window: hidden from Alt+Tab
        var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (IntPtr)(((long)exStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW));

        // Owned by the desktop window: survives Win+D and sits right above the desktop icons
        _desktop = FindDesktopWindow();
        if (_desktop != IntPtr.Zero)
            SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, _desktop);

        if (SlotAboveDesktop(hwnd) is { } after)
            SetWindowPos(hwnd, after, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private static IntPtr _desktop;

    /// <summary>
    /// The window to put this one under so it ends up just above the desktop
    /// (SetWindowPos places a window below its "insert after" window). Null if it's already there.
    /// "Bottom" would be wrong: it can slide under the desktop itself and disappear.
    /// </summary>
    private static IntPtr? SlotAboveDesktop(IntPtr hwnd)
    {
        if (_desktop == IntPtr.Zero)
            return HWND_BOTTOM;
        var above = GetWindow(_desktop, GW_HWNDPREV);
        if (above == hwnd)
            return null;
        return above == IntPtr.Zero ? HWND_TOP : above;
    }

    /// <summary>The top-level window that holds the desktop icons (Progman, or a WorkerW on older setups).</summary>
    public static IntPtr FindDesktopWindow()
    {
        var progman = FindWindow("Progman", null);
        if (progman != IntPtr.Zero && FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
            return progman;

        // With a wallpaper slideshow, the icons can live in a WorkerW instead.
        var found = IntPtr.Zero;
        EnumWindows((top, _) =>
        {
            if (FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) == IntPtr.Zero)
                return true;
            found = top;
            return false;
        }, IntPtr.Zero);
        return found != IntPtr.Zero ? found : progman;
    }

    private static IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_WINDOWPOSCHANGING)
        {
            // Whatever Windows wants (e.g. after a click), keep the widget just above the desktop,
            // behind every normal window.
            var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
            if ((pos.flags & SWP_NOZORDER) == 0)
            {
                if (SlotAboveDesktop(hwnd) is { } after)
                    pos.hwndInsertAfter = after;
                else
                    pos.flags |= SWP_NOZORDER; // already in place
                Marshal.StructureToPtr(pos, lParam, false);
            }
        }
        else if (msg == TaskbarCreated)
        {
            // Explorer restarted: the desktop window is new, so pin again.
            Pin(hwnd);
        }
        return IntPtr.Zero;
    }

    // ---------- Win32 ----------

    private const int GWL_EXSTYLE = -20;
    private const int GWLP_HWNDPARENT = -8;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const long WS_EX_APPWINDOW = 0x00040000;
    private const int WM_WINDOWPOSCHANGING = 0x0046;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint GW_HWNDPREV = 3;
    private static readonly IntPtr HWND_BOTTOM = new(1);
    private static readonly IntPtr HWND_TOP = IntPtr.Zero;

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x, y, cx, cy;
        public uint flags;
    }

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string? className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterWindowMessage(string message);
}
