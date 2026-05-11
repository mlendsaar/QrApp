# Architecture

## Overview

QrApp is a single-process WPF application targeting `net8.0-windows`. The app has no visible main window; instead it lives in the system tray and shows a floating overlay on demand.

```
┌─────────────────────────────────────────────────────┐
│                     QrApp Process                   │
│                                                     │
│  ┌──────────────┐    triggers    ┌───────────────┐  │
│  │ HotkeyService│───────────────▶│SelectionService│  │
│  └──────────────┘                └───────┬───────┘  │
│                                          │ text      │
│                                          ▼           │
│                                  ┌───────────────┐  │
│                                  │ QrCodeService │  │
│                                  └───────┬───────┘  │
│                                          │ BitmapSrc │
│                                          ▼           │
│                                  ┌───────────────┐  │
│                                  │OverlayWindow  │  │
│                                  │(WPF Popup)    │  │
│                                  └───────────────┘  │
│                                                     │
│  ┌──────────────────────────────────────────────┐   │
│  │  System Tray (NotifyIcon via WPF/Win32)      │   │
│  └──────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

## Components

### App.xaml.cs — Application Bootstrap

- Sets `ShutdownMode = OnExplicitShutdown` so the app keeps running with no visible window.
- Creates and wires together `HotkeyService`, `SelectionService`, `QrCodeService`.
- Owns the `NotifyIcon` (system tray) and its context menu (Settings, Quit).
- Subscribes to `HotkeyService.HotkeyPressed` and orchestrates the capture→generate→display flow.

### HotkeyService

Registers a system-wide hotkey using the Win32 `RegisterHotKey` / `UnregisterHotKey` APIs via a hidden `HwndSource` (message-only window). Exposes:

```csharp
event EventHandler HotkeyPressed;
void Register(ModifierKeys modifiers, Key key);
void Unregister();
```

**Why a message-only window?**  
`RegisterHotKey` requires a window handle (`HWND`) to post `WM_HOTKEY` messages to. A `HwndSource` created with `HWND_MESSAGE` as parent is the standard WPF pattern; it uses no screen real-estate.

### SelectionService

Captures currently selected text without requiring the user to manually copy it first.

**Algorithm:**

1. Save current clipboard contents.
2. Clear the clipboard.
3. Call `SendInput` to synthesize `Ctrl+C` (key-down + key-up for both VK_CONTROL and VK_C).
4. Wait up to 300 ms polling `Clipboard.ContainsText()`.
5. Read the text; restore original clipboard contents.
6. Return the captured string (empty string if nothing was selected).

**Edge cases:**

| Scenario | Handling |
|---|---|
| App that ignores Ctrl+C | Returns empty string; overlay shows an error message |
| Clipboard locked by another process | Retries up to 5× with 20 ms delay (Win32 `OpenClipboard` contention) |
| Selection contains only whitespace | Treated as empty; no overlay shown |
| Selection > 4 KB | QR code uses highest error-correction level; warns user if data exceeds QR v40 limit (~7 KB alphanumeric) |

**P/Invoke surface** (all in `NativeMethods.cs`):

```csharp
[DllImport("user32.dll")] static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
[DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
[DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
[DllImport("user32.dll")] static extern bool GetCursorPos(out POINT lpPoint);
```

### QrCodeService

Thin wrapper around the [QRCoder](https://github.com/codebude/QRCoder) library.

```csharp
BitmapSource Generate(string text, QrSettings settings);
```

- Chooses `ECCLevel.Q` (25% error correction) by default — good balance between size and robustness.
- Renders to a `PngByteQRCode` → `byte[]` → `BitmapImage` (in-memory, no temp files).
- Exposed `QrSettings` record: `ForegroundColor`, `BackgroundColor`, `PixelsPerModule` (default 10), `ECCLevel`.

### OverlayWindow

A borderless, `AllowsTransparency="True"` WPF `Window` that:

- Positions itself near the current mouse cursor (`GetCursorPos` P/Invoke), clamped to the current screen bounds.
- Binds to `OverlayViewModel` which exposes `QrImage` (`BitmapSource`) and `StatusText`.
- Handles `Deactivated` → auto-close (configurable, default on).
- Buttons: **Copy Image** (puts `BitmapSource` on clipboard), **Save PNG** (`SaveFileDialog`), **Close** (`Esc` key).
- Uses a `DispatcherTimer` for optional auto-dismiss after N seconds.

### Settings

Stored as JSON at `%APPDATA%\QrApp\settings.json` and loaded/saved by a simple `SettingsService` using `System.Text.Json`.

```json
{
  "hotkey": { "modifiers": "Ctrl+Shift", "key": "Q" },
  "qr": {
    "foreground": "#000000",
    "background": "#FFFFFF",
    "pixelsPerModule": 10,
    "eccLevel": "Q"
  },
  "overlay": {
    "autoDismissSeconds": 0,
    "showOnSecondMonitor": false
  }
}
```

## Threading Model

| Thread | Responsibilities |
|---|---|
| UI thread (STA) | All WPF, `HwndSource`, `Clipboard` access, `SendInput` |
| Background Task | None in v1 — QR generation is fast enough (<5 ms) to run on UI thread |

`SelectionService` must run on the UI thread because both `SendInput` and `Clipboard` have STA/thread-affinity requirements on Windows.

## Error Handling Strategy

- Empty selection → show tray tooltip "Nothing selected" for 2 s; no overlay.
- QR generation failure (text too long) → overlay shows an error label instead of the image.
- Hotkey already taken by another app → show tray balloon tip with instructions to change hotkey in settings.
- All unhandled exceptions → caught in `Application.DispatcherUnhandledException`, logged to `%APPDATA%\QrApp\error.log`, shown as tray balloon.

## Security Considerations

- The app synthesizes `Ctrl+C` via `SendInput`, which is restricted by UIPI (User Interface Privilege Isolation) — it cannot interact with elevated windows (Task Manager, UAC dialogs). This is by design; the app does not request elevation.
- Clipboard contents are restored after capture to avoid leaking sensitive data.
- No network access; all QR generation is offline and local.

## Dependency Graph

```
QrApp.csproj
├── QRCoder (NuGet, MIT)
├── System.Drawing.Common (NuGet, for BitmapSource helpers on .NET 8)
└── Microsoft.Xaml.Behaviors.Wpf (NuGet, for MVVM behaviors)
```

No transitive dependencies with native binaries.

## Future Considerations

- **WinUI 3 migration**: If WinUI 3 stabilises the `AppWindow` / overlay APIs, migrating would give a more modern look without changing core logic.
- **Background thread for generation**: If QR payload grows (e.g. binary data), move `QrCodeService.Generate` off the UI thread with `Task.Run` + dispatcher marshal-back.
- **Accessibility**: Overlay `Image` should have `AutomationProperties.Name` set to the encoded text so screen readers can announce it.
