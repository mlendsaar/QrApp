using System.Runtime.InteropServices;
using System.Windows;

namespace QrApp;

internal sealed class SelectionService
{
    public async Task<string> GetSelectedTextAsync()
    {
        IDataObject? saved = null;
        try { saved = Clipboard.GetDataObject(); } catch { }

        Clipboard.Clear();
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
            catch { await Task.Delay(20); } // clipboard contention retry
        }

        try { if (saved is not null) Clipboard.SetDataObject(saved, true); } catch { }
        return result.Trim();
    }

    private static void SendCtrlC()
    {
        var inputs = new NativeMethods.INPUT[4];
        inputs[0].type = NativeMethods.INPUT_KEYBOARD;
        inputs[0].ki.wVk = 0x11; // VK_CONTROL down

        inputs[1].type = NativeMethods.INPUT_KEYBOARD;
        inputs[1].ki.wVk = NativeMethods.VK_C; // VK_C down

        inputs[2].type = NativeMethods.INPUT_KEYBOARD;
        inputs[2].ki.wVk = NativeMethods.VK_C;
        inputs[2].ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP; // VK_C up

        inputs[3].type = NativeMethods.INPUT_KEYBOARD;
        inputs[3].ki.wVk = 0x11;
        inputs[3].ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP; // VK_CONTROL up

        NativeMethods.SendInput(4, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
