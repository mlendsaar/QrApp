using System.Runtime.InteropServices;
using System.Windows;

namespace QrApp;

internal sealed class SelectionService
{
    // Reads text directly from the clipboard. The user copies text with Ctrl+C first,
    // then presses the hotkey — no synthetic input needed.
    public string GetClipboardText()
    {
        // Another process may hold the clipboard open (e.g. RDP clip-sync, password
        // managers). OpenClipboard then fails with CLIPBRD_E_CANT_OPEN (0x800401D0).
        // Retry 8 × 25 ms = 200 ms before giving up — well under the user's perception.
        for (int i = 0; i < 8; i++)
        {
            try { return Clipboard.ContainsText() ? Clipboard.GetText().Trim() : string.Empty; }
            catch (COMException) { }
            catch (ExternalException) { }
            System.Threading.Thread.Sleep(25);
        }
        return string.Empty;
    }
}
