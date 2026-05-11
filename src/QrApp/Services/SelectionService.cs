using System.Runtime.InteropServices;
using System.Windows;

namespace QrApp;

internal sealed class SelectionService
{
    public async Task<string> GetSelectedTextAsync()
    {
        IDataObject? saved = null;
        try { saved = Clipboard.GetDataObject(); } catch { }

        // Clipboard may be locked briefly by another process; retry before giving up
        if (!await TryClipboardActionAsync(() => Clipboard.Clear()))
            return string.Empty;

        SendCtrlC();

        var deadline = DateTime.UtcNow.AddMilliseconds(300);
        string result = string.Empty;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(30);
            try
            {
                if (Clipboard.ContainsText()) { result = Clipboard.GetText(); break; }
            }
            catch { await Task.Delay(20); }
        }

        try { if (saved is not null) Clipboard.SetDataObject(saved, true); } catch { }
        return result.Trim();
    }

    // Retries a clipboard action up to 8 times with 25 ms back-off between attempts.
    private static async Task<bool> TryClipboardActionAsync(Action action)
    {
        for (int i = 0; i < 8; i++)
        {
            try { action(); return true; }
            catch (COMException) { }
            catch (ExternalException) { }
            await Task.Delay(25);
        }
        return false;
    }

    private static void SendCtrlC()
    {
        // The hotkey combo (e.g. Ctrl+Shift+Q) leaves modifier keys physically held when
        // this runs. Release Shift / Alt / Win so the target app sees plain Ctrl+C, not
        // Ctrl+Shift+C (which opens DevTools in Chrome instead of copying).
        ushort[] interferingKeys = [0x10, 0x12, 0x5B, 0x5C]; // Shift, Alt, LWin, RWin
        var toRelease = interferingKeys
            .Where(vk => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0)
            .ToArray();

        var inputs = new NativeMethods.INPUT[toRelease.Length + 4];
        int i = 0;

        foreach (var vk in toRelease)
        {
            inputs[i].type = NativeMethods.INPUT_KEYBOARD;
            inputs[i].ki.wVk = vk;
            inputs[i].ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;
            i++;
        }

        inputs[i].type = NativeMethods.INPUT_KEYBOARD;
        inputs[i].ki.wVk = 0x11; i++;                                    // Ctrl down
        inputs[i].type = NativeMethods.INPUT_KEYBOARD;
        inputs[i].ki.wVk = NativeMethods.VK_C; i++;                      // C down
        inputs[i].type = NativeMethods.INPUT_KEYBOARD;
        inputs[i].ki.wVk = NativeMethods.VK_C;
        inputs[i].ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP; i++;       // C up
        inputs[i].type = NativeMethods.INPUT_KEYBOARD;
        inputs[i].ki.wVk = 0x11;
        inputs[i].ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;            // Ctrl up

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
