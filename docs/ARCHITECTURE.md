# Architecture

## Overview

QrApp is a single-process WPF application targeting `net8.0-windows10.0.22000.0` (Windows 11). It lives in the system tray with no visible main window and shows a floating overlay on demand.

```
┌──────────────────────────────────────────────────────────────────────┐
│                           QrApp Process                              │
│                                                                      │
│  ┌───────────────┐  hotkey   ┌──────────────────────────────────┐   │
│  │ HotkeyService │──────────▶│  SelectionService                │   │
│  └───────────────┘           │  (Ctrl+C → clipboard poll)       │   │
│                              │                                  │   │
│                              │  empty? → "Nothing to encode"    │   │
│                              └─────────────┬────────────────────┘   │
│                                            │ raw text                │
│                                            ▼                         │
│                           ┌──────────────────────┐                  │
│                           │ TextSanitizerService  │                  │
│                           └──────────┬───────────┘                  │
│                                      │ clean text                    │
│                                      ▼                               │
│                              ┌───────────────┐                      │
│                              │ QrCodeService │                      │
│                              └───────┬───────┘                      │
│                                      │ BitmapSource                  │
│                                      ▼                               │
│                        ┌─────────────────────────────┐              │
│                        │        OverlayWindow          │             │
│                        │  [TextBox] [QR]               │             │
│                        │  [OCR btn — hidden by default]│             │
│                        └──────────┬──────────────────┘              │
│                                   │ OCR button click (manual only)   │
│                                   ▼                                  │
│                        ┌──────────────────────────┐                 │
│                        │  RegionSelectorWindow     │                 │
│                        │  (fullscreen, snip-style) │                 │
│                        └──────────┬───────────────┘                 │
│                                   │ Rectangle                        │
│                                   ▼                                  │
│                        OcrService.RecognizeRegionAsync()             │
│                        → TextSanitizerService → QrCodeService        │
│                        → OverlayWindow (updated)                     │
│                                                                      │
│  ┌────────────────────────────────────────────────────────────┐     │
│  │  Tray Icon  →  right-click  →  Settings / Quit             │     │
│  └────────────────────────────────────────────────────────────┘     │
└──────────────────────────────────────────────────────────────────────┘
```

---

## Components

### App.xaml.cs — Application Bootstrap

- Sets `ShutdownMode = OnExplicitShutdown` so the app runs without a visible window.
- Composes all services: `HotkeyService`, `SelectionService`, `OcrService`, `TextSanitizerService`, `QrCodeService`, `SettingsService`.
- Owns `NotifyIcon` (tray) and its right-click menu (Settings, Quit).
- `HotkeyService.HotkeyPressed` → `RunCapturePipelineAsync()`: closes any existing overlay first, then runs the pipeline.
- `RunCapturePipelineAsync()`: calls `SelectionService` → sanitize → generate → show overlay. If nothing captured, shows tray balloon "Nothing to encode". No automatic OCR fallback.
- The app is completely passive until the hotkey is pressed — no background hooks or monitors.
- On startup: applies autostart registry setting from `SettingsService`.

### HotkeyService

Registers a system-wide hotkey via `RegisterHotKey` / `UnregisterHotKey` on a hidden `HwndSource` (message-only window).

```csharp
event EventHandler HotkeyPressed;
void Register(ModifierKeys modifiers, Key key);
void Unregister();
```

`HwndSource` with `HWND_MESSAGE` parent provides a `HWND` for `WM_HOTKEY` messages without any screen presence. Hotkey is re-registered immediately when the user changes it in Settings and clicks Apply.

**Behaviour when overlay is open:** App closes the existing `OverlayWindow`, then runs the capture pipeline to open a fresh one. This avoids overlapping overlays.

### SelectionService

Captures the currently selected text by synthesising `Ctrl+C`.

**Algorithm:**

1. Save clipboard contents.
2. Clear clipboard (with retry — see below).
3. `SendInput`: VK_CONTROL down → VK_C down → VK_C up → VK_CONTROL up.
4. Poll `Clipboard.ContainsText()` for up to 300 ms.
5. Read text; restore clipboard.
6. Return text, or empty string if nothing captured.

**Clipboard retry:** All `Clipboard.*` entry points can throw `COMException 0x800401D0` (`CLIPBRD_E_CANT_OPEN`) when another process (antivirus, clipboard manager, etc.) holds the clipboard. A `TryClipboardActionAsync` helper retries up to 8 times with 25 ms back-off before giving up and returning empty.

**Edge cases:**

| Scenario | Handling |
|---|---|
| App ignores Ctrl+C | Returns empty → "Nothing to encode" tray notification |
| Clipboard locked | Retry 8× with 25 ms back-off; return empty on all failures |
| Only whitespace selected | Treated as empty after sanitization |
| Text exceeds QR v40 limit | `QrCodeService` throws; overlay shows error |

### OcrService

One mode — manual region selection. There is no automatic OCR fallback; OCR is only triggered when the user explicitly clicks the OCR button in the overlay.

**Manual region** (called after user draws a region in `RegionSelectorWindow`):

1. Receive `System.Drawing.Rectangle` from `RegionSelectorWindow`.
2. Capture exactly that screen rectangle.
3. Same OCR pipeline as above.

```csharp
internal sealed class OcrService
{
    private readonly OcrEngine _engine =
        OcrEngine.TryCreateFromUserProfileLanguages() ??
        OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en"));

    public Task<string> RecognizeRegionAsync(System.Drawing.Rectangle screenRect) { ... }

    private async Task<string> RecognizeBitmapAsync(System.Drawing.Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Seek(0, SeekOrigin.Begin);
        var decoder = await BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
        var soft = await decoder.GetSoftwareBitmapAsync();
        var result = await _engine.RecognizeAsync(soft);
        return string.Join(" ", result.Lines.Select(l => l.Text));
    }
}
```

`OcrEngine` is created once and reused. `Windows.Media.Ocr` is available via the `net8.0-windows10.0.22000.0` TFM with no extra NuGet package.

### RegionSelectorWindow

A fullscreen, borderless, `AllowsTransparency="True"` WPF window used for manual OCR region selection. Shown when the user clicks the OCR button in the overlay.

**Behaviour:**

1. Overlay hides itself before showing this window.
2. Window covers all monitors (use `SystemParameters.VirtualScreen*` bounds).
3. Background: semi-transparent dark overlay (40% opacity black).
4. Mouse down: record start point, begin drawing selection rectangle.
5. Mouse move: update rectangle; fill is slightly lighter than overlay; border is white.
6. Mouse up: return the screen `Rectangle` to the caller via `TaskCompletionSource<Rectangle?>`.
7. `Esc`: cancel — sets result to `null`; overlay re-shows with previous content unchanged.
8. Cursor: `Cursors.Cross`.

```csharp
internal sealed partial class RegionSelectorWindow : Window
{
    private readonly TaskCompletionSource<System.Drawing.Rectangle?> _tcs;

    public static Task<System.Drawing.Rectangle?> SelectAsync()
    {
        var tcs = new TaskCompletionSource<System.Drawing.Rectangle?>();
        new RegionSelectorWindow(tcs).Show();
        return tcs.Task;
    }
}
```

### TextSanitizerService

Applied to every string (from `SelectionService` or `OcrService`) before it reaches `QrCodeService` or the overlay `TextBox`.

```csharp
record SanitizerRule(string Match, string Replace, bool IsRegex = false);

internal sealed class TextSanitizerService
{
    private readonly IReadOnlyList<SanitizerRule> _rules;

    public string Sanitize(string input)
    {
        foreach (var rule in _rules)
            input = rule.IsRegex
                ? Regex.Replace(input, rule.Match, rule.Replace)
                : input.Replace(rule.Match, rule.Replace);
        return input.Trim();
    }
}
```

Regex instances are compiled and cached on construction. Default rules: strip BOM, zero-width spaces, soft hyphens, null bytes; normalise CRLF → LF; strip trailing whitespace per line.

**Temporary edits:** the overlay `TextBox` is two-way bound to `OverlayViewModel.SourceText`. Edits trigger `QrCodeService.Generate` via a 150 ms debounced `PropertyChanged` handler.

### QrCodeService

```csharp
internal sealed class QrCodeService
{
    public BitmapSource Generate(string text, QrSettings settings)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, settings.EccLevel);
        int moduleCount = data.ModuleMatrix.Count;
        int ppm = Math.Max(1, (int)Math.Ceiling((double)settings.TargetSizePx / moduleCount));
        using var code = new PngByteQRCode(data);
        byte[] png = code.GetGraphic(ppm);          // default black on white

        var image = new BitmapImage();
        using var ms = new MemoryStream(png);
        image.BeginInit();
        image.CacheOption  = BitmapCacheOption.OnLoad;
        image.StreamSource = ms;
        image.EndInit();
        image.Freeze();
        return image;
    }
}

record QrSettings(int TargetSizePx = 300, QRCodeGenerator.ECCLevel EccLevel = QRCodeGenerator.ECCLevel.Q);
```

`PixelsPerModule` is derived from `TargetSizePx` divided by the actual module count of the generated QR data — so the output closely matches the requested size regardless of QR version. Output is always black on white.

### OverlayWindow

Borderless, `AllowsTransparency="True"` WPF window. Positioned 16 px to the right of the mouse cursor, clamped to the current monitor's work area; flips left if insufficient space.

- Two-column layout: editable `TextBox` (left) + QR `Image` (right).
- **OCR button** in the header bar (hidden by default; controlled by `Overlay.ShowOcrButton` setting) — clicking it:
  1. Hides `OverlayWindow`.
  2. Awaits `RegionSelectorWindow.SelectAsync()`.
  3. If cancelled: re-shows with previous content.
  4. If rect received: calls `OcrService.RecognizeRegionAsync(rect)` → sanitize → generate → updates `OverlayViewModel` → re-shows.
- `TextBox` edits regenerate QR with 150 ms debounce.
- Status bar (single line, full width): hidden by default; amber warning at 80–100% capacity; red error above 100%.
- Error message text: `"Too much data — edit the text to reduce it."`
- Warning message text: `"Approaching QR capacity — consider reducing text or switching to ECC L."`
- Dismiss: focus lost, `Esc`, or auto-dismiss timer.
- `Deactivated` handler checks if `RegionSelectorWindow` is the newly focused window before closing — if so, does not auto-close.

### SettingsWindow

Standard WPF window, opened from the tray right-click menu. Binds to `SettingsViewModel` (working copy pattern — changes only persist on Apply).

| Control | Setting |
|---|---|
| Press-to-record `TextBox` | Hotkey; re-registers immediately on Apply |
| `Slider` 200–600, step 50 | QR target size (px); live preview thumbnail |
| `ComboBox` L/M/Q/H | ECC level |
| `CheckBox` + seconds field | Overlay auto-dismiss |
| Toggle switch | Show OCR Region button in overlay (default: off) |
| `CheckBox` | Launch at Windows startup |
| Editable rule list | Symbol filter rules (match, replace, regex, delete) |

Apply saves `settings.json` and applies all changes live. Cancel discards the working copy.

### SettingsService

Loads `%APPDATA%\QrApp\settings.json` on startup; saves on Apply.

**Corruption handling:** if `settings.json` is missing, unreadable, or fails JSON deserialization, the service silently resets to `AppSettings.Default` and overwrites the file.

```csharp
internal sealed class SettingsService
{
    private static readonly string Path =
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QrApp", "settings.json");

    public AppSettings Load()
    {
        try
        {
            var json = File.ReadAllText(Path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? AppSettings.Default;
        }
        catch   // file missing, locked, or malformed JSON
        {
            var defaults = AppSettings.Default;
            Save(defaults);
            return defaults;
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        File.WriteAllText(Path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void ApplyAutostart(bool enable)
    {
        const string key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        using var reg = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(key, writable: true);
        if (enable)
            reg?.SetValue("QrApp", Environment.ProcessPath!);
        else
            reg?.DeleteValue("QrApp", throwOnMissingValue: false);
    }
}
```

### Settings Schema

```json
{
  "hotkey": { "modifiers": "Ctrl+Shift", "key": "Q" },
  "qr": {
    "targetSizePx": 300,
    "eccLevel": "Q"
  },
  "overlay": {
    "autoDismissSeconds": 0,
    "showOcrButton": false
  },
  "autostart": true,
  "sanitizer": {
    "rules": [
      { "match": "\\uFEFF", "replace": "" },
      { "match": "\\u200B", "replace": "" },
      { "match": "\\u00AD", "replace": "" },
      { "match": "\\u0000", "replace": "" },
      { "match": "\r\n",    "replace": "\n" },
      { "match": "\\s+$",   "replace": "", "regex": true }
    ]
  }
}
```

Unicode escape sequences are used in JSON to avoid invisible characters in the file. `SettingsService` converts them to actual characters before passing to `TextSanitizerService`.

---

## QR Code Version Reference

| Version | Grid | Max bytes (ECC L) | Max bytes (ECC Q) | Max bytes (ECC H) |
|---|---|---|---|---|
| 1 | 21×21 | 17 | 13 | 7 |
| 5 | 37×37 | 108 | 85 | 48 |
| 10 | 57×57 | 346 | 271 | 154 |
| 20 | 97×97 | 1 273 | 812 | 520 |
| 40 | 177×177 | 2 953 | 1 663 | 1 273 |

QRCoder auto-selects the version. ECC Q (default): 25% damage recovery. Dropping to ECC L raises v40 capacity to 2 953 bytes.

---

## Threading Model

| Thread | Responsibilities |
|---|---|
| UI thread (STA) | WPF, `HwndSource`, `Clipboard`, `SendInput`, `OcrService`, `RegionSelectorWindow` |
| None (v1) | QR generation is < 20 ms on the UI thread; no background task needed |

`SelectionService` and `OcrService` require STA: `SendInput`, `Clipboard`, `Graphics.CopyFromScreen`, and WinRT `SoftwareBitmap` conversion all have STA affinity.

---

## Error Handling

| Condition | Behaviour |
|---|---|
| Empty selection (hotkey or double-click) | Tray balloon "Nothing to encode" for 2 s; no overlay |
| `CLIPBRD_E_CANT_OPEN` | Retry 8× / 25 ms; return empty on persistent failure |
| Text > v40 capacity | Overlay error: "Too much data — edit the text to reduce it." |
| Text 80–100% of capacity | Overlay warning: "Approaching QR capacity — consider reducing text or switching to ECC L." |
| Hotkey already registered | Tray balloon: "Hotkey in use — change it in Settings" |
| `settings.json` corrupted | Reset to defaults silently, overwrite file |
| OCR region cancelled | Overlay re-shows with previous content unchanged |

---

## Security Considerations

- `SendInput` is restricted by UIPI — cannot interact with elevated windows (Task Manager, UAC prompts). By design; the app does not request elevation.
- Clipboard is restored after every capture to avoid leaving sensitive data on it.
- All processing (QR generation, OCR) is fully local; no data leaves the machine.

---

## Non-Functional Requirements

| Requirement | Decision |
|---|---|
| OS target | Windows 11 only (`net8.0-windows10.0.22000.0`, `SupportedOSPlatformVersion=10.0.22000.0`) |
| SDK | .NET 8 LTS (Nov 2022 – Nov 2026); migrate to .NET 10 LTS when released |
| Distribution | Self-contained single-file EXE (`--self-contained true -r win-x64`); manual distribution of build output |
| Runtime prerequisites | None — runtime is bundled (~70 MB EXE) |
| Autostart | Registry key `HKCU\...\Run` written on first launch (default on); togglable in Settings |
| Elevation | Not required; all Win32 APIs work at standard user privilege |
| Internet | Not required; all features are local and offline |
| Updates | Manual — user downloads and replaces EXE |
| Settings corruption | Reset to defaults, overwrite file |
| Logging | Not in scope for v1 |

---

## Performance Budget

| Operation | Target |
|---|---|
| Hotkey → overlay visible | < 250 ms (including clipboard capture and QR generation) |
| QR regeneration on text edit | < 50 ms (debounced 150 ms before triggering) |
| OCR manual region | < 500 ms after user releases mouse |
| Settings window open | < 100 ms |

---

## Acceptance Criteria (v1.0)

The application ships when:

- Built as a self-contained single-file EXE with `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`.
- Runs on a clean Windows 11 installation with no additional prerequisites.
- All functional requirements are verified by manual testing.
- Autostarts on login by default.
- Settings survive app restart; corrupted settings file is auto-recovered.

---

## Dependency Graph

```
QrApp.csproj  (net8.0-windows10.0.22000.0)
├── QRCoder (NuGet, MIT)
├── System.Drawing.Common (NuGet, screen capture bitmap)
└── Microsoft.Xaml.Behaviors.Wpf (NuGet, MVVM behaviors)
```

`Windows.Media.Ocr` is available directly through the TFM — no extra NuGet package required.

---

## Future Considerations

- **.NET 10 LTS migration** (Nov 2025): no breaking changes expected.
- **WinUI 3**: better overlay/AppWindow APIs and native Mica; revisit when stable.
- **Accessibility**: `AutomationProperties.Name` on the overlay `Image` for screen readers.
- **Multi-monitor**: RegionSelectorWindow already spans virtual screen; verify DPI scaling per-monitor.
