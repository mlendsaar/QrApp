using System.Runtime.InteropServices;

namespace QrApp;

internal static class NativeMethods
{
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] internal static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
    [DllImport("user32.dll")] internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    internal const int  WM_HOTKEY             = 0x0312;
    internal const int  MOD_CONTROL           = 0x0002;
    internal const int  MOD_SHIFT             = 0x0004;
    internal const int  VK_C                  = 0x43;
    internal const uint INPUT_KEYBOARD        = 1;
    internal const uint KEYEVENTF_KEYUP       = 0x0002;
    internal const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)] internal struct POINT  { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct RECT   { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)]
    internal struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(4)] public KEYBDINPUT ki; }
}
