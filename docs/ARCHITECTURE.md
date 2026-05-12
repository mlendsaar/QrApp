# Architecture

## Overview

QrApp is a single-process WPF application targeting `net8.0-windows10.0.22000.0` (Windows 11). It lives in the system tray with no visible main window and shows a floating overlay on demand.

```
┌──────────────────────────────────────────────────────────────────────┐
│                           QrApp Process                              │
│                                                                      │
│  ┌───────────────┐  hotkey   ┌──────────────────────────────────┐   │
│  │ HotkeyService │──────────▶│  SelectionService                │   │
│  └───────────────┘           │  (reads Clipboard directly)      │   │
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

Reads text directly from the system clipboard. The user copies with `Ctrl+C` *before* pressing the hotkey — no input synthesis is performed.

**Algorithm:**

1. Call `Clipboard.ContainsText()` / `Clipboard.GetText()`.
2. On `COMException` / `ExternalException` (clipboard locked), wait 25 ms and retry.
3. After 8 failed retries (~200 ms total), return empty string.

**Why no `SendInput`:** an earlier iteration synthesised `Ctrl+C` from the hotkey handler, but the modifiers from the hotkey itself contaminated the injected keystrokes (e.g. `Ctrl+Shift+Q` made Chrome receive `Ctrl+Shift+C` → DevTools) and different applications responded differently. Reading the clipboard directly is simpler, faster, and works in every app.

**Clipboard retry:** `Clipboard.*` entry points can throw `COMException 0x800401D0` (`CLIPBRD_E_CANT_OPEN`) when another process (RDP clip-sync, antivirus, clipboard manager) holds the clipboard. The retry loop (8 × 25 ms) covers transient locks.

**Edge cases:**

| Scenario | Handling |
|---|---|
| Nothing copied (clipboard empty or non-text) | Returns empty → "Nothing to encode" tray notification |
| Clipboard locked | Retry 8× with 25 ms back-off; return empty on all failures |
| Only whitespace on clipboard | Treated as empty after sanitization |
| Text exceeds QR v40 limit | `QrCodeService` throws; overlay shows error |

### OcrService

One mode — manual region selection. There is no automatic OCR fallback; OCR is only triggered when the user explicitly clicks the OCR button in the overlay.

**Manual region** (called after user draws a region in `RegionSelectorWindow`):

1. Receive `System.Drawing.Rectangle` from `RegionSelectorWindow`.
2. Capture exactly that screen rectangle into a `Bitmap`.
3. If `OcrConfig.UpscaleEnabled` (default `true`), bicubic-upscale the bitmap (max 3×, clamped to ≤ 4800 px to stay under the Windows OCR engine's 5000 px input limit). Small text recognises far more reliably after upscale.
4. Encode to PNG → decode via `BitmapDecoder` → `SoftwareBitmap`.
5. `OcrEngine.RecognizeAsync`; join recognised lines with `\n` if `OcrConfig.PreserveLines`, otherwise with a single space.

```csharp
internal sealed class OcrService
{
    private readonly OcrEngine _engine =
        OcrEngine.TryCreateFromUserProfileLanguages() ??
        OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en"))!;

    public Task<string> RecognizeRegionAsync(System.Drawing.Rectangle screenRect,
                                             OcrConfig? config = null) { ... }
}

sealed class OcrConfig
{
    public bool UpscaleEnabled { get; set; } = true;
    public bool PreserveLines  { get; set; } = true;
}
```

`OcrEngine` is created once and reused. `Windows.Media.Ocr` is available via the `net8.0-windows10.0.22000.0` TFM with no extra NuGet package. The engine prefers the user's installed language packs (`TryCreateFromUserProfileLanguages`) and falls back to English on minimal Windows installs.

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
    private readonly IReadOnlyList<(string match, string replace, Regex? regex)> _rules;

    public TextSanitizerService(IEnumerable<SanitizerRule> rules)
    {
        // Pre-compile regex rules at construction so the capture hot-path
        // doesn't re-parse on every keystroke / capture.
        _rules = rules.Select(r => (
            r.Match,
            r.Replace,
            r.IsRegex ? new Regex(r.Match, RegexOptions.Compiled | RegexOptions.Multiline) : null
        )).ToList();
    }

    public string Sanitize(string input)
    {
        foreach (var (match, replace, regex) in _rules)
            input = regex is not null
                ? regex.Replace(input, replace)
                : input.Replace(match, replace, StringComparison.Ordinal);
        return input.Trim();
    }
}
```

Default rules strip invisible characters that web copy often inflates the QR payload with:

- `U+FEFF` BOM
- `U+200B` zero-width space
- `U+00AD` soft hyphen
- `U+00A0` non-breaking space → regular space

Plus `\r\n` → `\n` and a regex `\s+$` to trim trailing whitespace per line.

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

Borderless, `AllowsTransparency="True"` WPF window. **Centred on the work area of the monitor that contains the cursor** at open time (uses `MonitorFromPoint` + `GetMonitorInfo`), so it never appears off-screen on multi-monitor setups.

**Window size is derived from `Qr.TargetSizePx`** in the `OverlayWindow` constructor so the QR image renders at the user's chosen pixel size. With both content columns set to `*`, the QR column ends up ≈ `TargetSizePx + 16` px wide, making `Stretch="Uniform"` paint the QR at (close to) its native size rather than down-scaling it into a fixed 268 px column.

- Two-column layout: editable `TextBox` (left) + QR `Image` (right).
- **Header bar**: draggable (`MouseLeftButtonDown` → `DragMove()`), excluding the button area, so the user can reposition before triggering an OCR region selection. Contains:
  - **OCR button** (optional, left): hidden by default; controlled by `Overlay.ShowOcrButton` setting.
  - **Help button** (`?`): opens `HelpWindow` with the embedded `JUHEND.md` user guide.
  - **Close button** (`✕`): hides the overlay (does not destroy it — see `OnDeactivated` note below).
- **OCR button click flow**:
  1. Sets `_suppressDeactivate = true` and hides the overlay.
  2. Awaits `RegionSelectorWindow.SelectAsync()`.
  3. If cancelled: re-shows with previous content.
  4. If rect received: calls `OcrService.RecognizeRegionAsync(rect, OcrConfig)` → sanitize → generate → updates `OverlayViewModel` → re-shows.
- `TextBox` edits regenerate QR with 150 ms debounce.
- Status bar (single line, full width): hidden by default; amber warning at 80–100% capacity; red error above 100%.
- Error message text: `"Too much data — edit the text to reduce it."`
- Warning message text: `"Approaching QR capacity — consider reducing text or switching to ECC L."`
- Dismiss: focus lost, `Esc`, or `✕` button — all call `Hide()` (not `Close()`) to avoid re-entrancy when the App also closes the overlay on hotkey re-press.
- **`_suppressDeactivate` flag**: set while a child window (Help, RegionSelector) is on top so the `OnDeactivated` handler does not auto-hide the overlay underneath it.

### SettingsWindow

Standard WPF window, opened from the tray right-click menu. Binds to `SettingsViewModel` (working copy pattern — changes only persist on Apply).

| Control | Setting |
|---|---|
| Press-to-record `TextBox` | Hotkey; re-registers immediately on Apply |
| `Slider` 200–600, step 50 | QR target size (px); live preview thumbnail |
| `ComboBox` L/M/Q/H | ECC level (with ⓘ button → opens `HelpWindow`) |
| `CheckBox` + seconds field | Overlay auto-dismiss |
| Toggle switch | Show OCR Region button in overlay (default: off) |
| Toggle switch | OCR upscale region before recognition (default: on) |
| Toggle switch | OCR preserve line breaks (default: on) |
| `CheckBox` | Launch at Windows startup |
| Editable rule list | Symbol filter rules (match, replace, regex, delete) |

Apply saves `settings.json` and applies all changes live. Cancel discards the working copy.

### HelpWindow

A standalone `Window` shown when the user clicks the `?` button in the overlay header or the ⓘ button next to ECC Level in Settings. Renders the Estonian user guide:

1. Reads `JUHEND.md` from the assembly as an `EmbeddedResource` — ships inside the single-file EXE.
2. Converts markdown → HTML via Markdig (`UseAdvancedExtensions`).
3. Wraps the HTML in a small inline stylesheet (Segoe UI, Win11 palette) and renders it in a WPF `WebBrowser` control via `NavigateToString`.

Embedding the docs as a resource means no companion file or installer is required.

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
  "hotkey": { "modifiers": "Control,Shift", "key": "F2" },
  "qr": {
    "targetSizePx": 300,
    "eccLevel": "Q"
  },
  "overlay": {
    "autoDismissSeconds": 0,
    "showOcrButton": false
  },
  "ocr": {
    "upscaleEnabled": true,
    "preserveLines": true
  },
  "autostart": true,
  "sanitizer": {
    "rules": [
      { "match": "﻿", "replace": "" },
      { "match": "​", "replace": "" },
      { "match": "­", "replace": "" },
      { "match": " ", "replace": " " },
      { "match": "\r\n",   "replace": "\n" },
      { "match": "\\s+$",  "replace": "", "isRegex": true }
    ]
  }
}
```

`System.Text.Json` serialises Unicode characters in their literal form (or as `\uXXXX` escapes, depending on the encoder), and `TextSanitizerService` consumes the `SanitizerRule` records as-is.

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
| UI thread (STA) | WPF, `HwndSource`, `Clipboard`, `OcrService`, `RegionSelectorWindow` |
| None (v1) | QR generation is < 20 ms on the UI thread; no background task needed |

`SelectionService` and `OcrService` require STA: `Clipboard`, `Graphics.CopyFromScreen`, and WinRT `SoftwareBitmap` conversion all have STA affinity.

---

## Error Handling

| Condition | Behaviour |
|---|---|
| Clipboard empty or non-text on hotkey | Tray balloon "Nothing to encode" for 2 s; no overlay |
| `CLIPBRD_E_CANT_OPEN` | Retry 8× / 25 ms; return empty on persistent failure |
| Text > v40 capacity | Overlay error: "Too much data — edit the text to reduce it." |
| Text 80–100% of capacity | Overlay warning: "Approaching QR capacity — consider reducing text or switching to ECC L." |
| Hotkey already registered | Tray balloon: "Hotkey in use — change it in Settings" |
| `settings.json` corrupted | Reset to defaults silently, overwrite file |
| OCR region cancelled | Overlay re-shows with previous content unchanged |

---

## Security Considerations

- No keyboard or mouse input is synthesised. The app only *reads* the clipboard (no clear/restore), so it cannot interfere with the user's clipboard history or password managers.
- All processing (QR generation, OCR) is fully local; no data leaves the machine.
- `settings.json` is written under `%APPDATA%\QrApp\` and the autostart entry under `HKCU\…\Run` — both per-user, no elevation required.
- The help content (`JUHEND.md`) is embedded in the EXE at build time; it is not loaded from any user-writable location at runtime.

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
