using System.Runtime.InteropServices;

namespace QrApp;

// Win32 P/Invoke surface. WPF has no built-in global-hotkey API, so we go to
// user32 directly. Monitor APIs are needed to position the overlay on the
// active monitor (WPF's SystemParameters returns only the primary screen).
internal static class NativeMethods
{
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] internal static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
    [DllImport("user32.dll")] internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    internal const int  WM_HOTKEY                = 0x0312; // posted to the hotkey-owning window when the registered combo is pressed
    internal const uint MONITOR_DEFAULTTONEAREST = 2;      // fall back to the nearest monitor when the point is off-screen

    [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct RECT  { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
}
