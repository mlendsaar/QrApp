using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace QrApp;

internal sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 9001;
    private HwndSource? _source;

    public event EventHandler? HotkeyPressed;

    public void Register(ModifierKeys modifiers, Key key)
    {
        Unregister();

        // Message-only window — no screen presence, just a target for WM_HOTKEY
        var parms = new HwndSourceParameters("QrApp_HotkeySource")
        {
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE
            WindowStyle  = 0,
            Width        = 0,
            Height       = 0,
        };
        _source = new HwndSource(parms);
        _source.AddHook(WndProc);

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (!NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, (uint)modifiers, vk))
            throw new InvalidOperationException("The hotkey is already registered by another application.");
    }

    public void Unregister()
    {
        if (_source is null) return;
        NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
        _source.RemoveHook(WndProc);
        _source.Dispose();
        _source = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose() => Unregister();
}
