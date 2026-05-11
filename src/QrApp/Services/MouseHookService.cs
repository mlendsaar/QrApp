namespace QrApp;

internal sealed class MouseHookService : IDisposable
{
    public event EventHandler? DoubleClicked;

    private readonly NativeMethods.LowLevelMouseProc _proc;
    private IntPtr _hookHandle;
    private DateTime _lastClickTime = DateTime.MinValue;

    public MouseHookService()
    {
        _proc = HookCallback; // must store delegate ref to prevent GC

        var moduleName = System.Diagnostics.Process.GetCurrentProcess().MainModule?.ModuleName;
        var hMod = NativeMethods.GetModuleHandle(moduleName);
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _proc, hMod, 0);
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)NativeMethods.WM_LBUTTONDOWN)
        {
            var now = DateTime.UtcNow;
            var threshold = TimeSpan.FromMilliseconds(NativeMethods.GetDoubleClickTime());
            if (now - _lastClickTime <= threshold)
            {
                _lastClickTime = DateTime.MinValue; // prevent triple-click triggering again
                DoubleClicked?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _lastClickTime = now;
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }
}
