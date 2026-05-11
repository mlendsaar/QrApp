# QrApp — Development Tasks

## How to Use

- Before starting a task: run `git status` and `git log --oneline -5` to verify it is not already done
- Mark tasks complete with `[x]` as you finish them
- After each completed task: `git add`, `git commit -m "..."`, `git push`
- If a task reveals sub-tasks, add them inline before proceeding

---

## Phase 1 — Project Setup

- [x] **Create solution and project scaffold**
  - `dotnet new sln -n QrApp`
  - `dotnet new wpf -n QrApp -o src/QrApp --framework net8.0-windows`
  - `dotnet sln add src/QrApp/QrApp.csproj`
  - Create folder tree: `Services/`, `ViewModels/`, `Helpers/`, `Assets/`

- [x] **Configure csproj**
  - TFM: `net8.0-windows10.0.22000.0`
  - `SupportedOSPlatformVersion`: `10.0.22000.0`
  - `SelfContained`, `RuntimeIdentifier=win-x64`, `PublishSingleFile`, `IncludeNativeLibrariesForSelfExtract`
  - Add NuGet: `QRCoder 1.6.0`, `System.Drawing.Common 8.0.0`, `Microsoft.Xaml.Behaviors.Wpf 1.1.77`

- [x] **Draw application icon** (`Assets/icon.ico`)
  - 256×256 main size; also embed 64×64, 32×32, 16×16 in the ICO container
  - Design: simple QR code glyph, dark squares on transparent/white background
  - Tool: any vector editor (Inkscape, Figma) → export PNG → convert to ICO with ImageMagick or IcoFX

- [x] **Draw tray icon** (`Assets/tray-icon.ico`)
  - Must be legible at 16×16 and 32×32
  - Design: minimal QR outline glyph; use system foreground color so it adapts to light/dark taskbar
  - Keep it simpler than the app icon — at 16 px, detail is invisible

---

## Phase 2 — Core Services

- [x] **`Helpers/NativeMethods.cs`**
  - P/Invoke: `RegisterHotKey`, `UnregisterHotKey`, `SendInput`, `GetCursorPos`, `MonitorFromPoint`, `GetMonitorInfo`
  - Structs: `POINT`, `RECT`, `MONITORINFO`, `KEYBDINPUT`, `INPUT`
  - Constants: `WM_HOTKEY`, `MOD_CONTROL`, `MOD_SHIFT`, `VK_C`, `INPUT_KEYBOARD`, `KEYEVENTF_KEYUP`, `MONITOR_DEFAULTTONEAREST`
  - Reference skeleton in `docs/DEVELOPMENT.md`

- [x] **`Services/HotkeyService.cs`**
  - `HwndSource` message-only window (`HWND_MESSAGE` parent)
  - `RegisterHotKey` / `UnregisterHotKey` with configurable modifiers + key
  - `WM_HOTKEY` → fire `HotkeyPressed` event
  - `IDisposable`: unregister on dispose
  - Verify: press hotkey in a running app, confirm event fires

- [x] **`Services/SettingsService.cs`**
  - Load `%APPDATA%\QrApp\settings.json` → `AppSettings`
  - On any exception (missing/locked/malformed): reset to `AppSettings.Default`, overwrite file
  - `Save(AppSettings)`: create directory if needed, write indented JSON
  - `ApplyAutostart(bool)`: write/delete `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run\QrApp`
  - Define `AppSettings` record with `Default` static property
  - Verify: delete the file → app resets; corrupt the file → app resets

- [x] **`Services/TextSanitizerService.cs`**
  - `SanitizerRule(string Match, string Replace, bool IsRegex)` record
  - Compile `Regex` instances at construction (not per-call)
  - `Sanitize(string)`: apply rules in order, then `Trim()`
  - Default rules (from `docs/ARCHITECTURE.md` Settings Schema section)
  - Verify: pass a string with BOM, CRLF, trailing spaces → confirm all stripped

- [x] **`Services/SelectionService.cs`**
  - Save clipboard → clear → `SendInput` Ctrl+C → poll 300 ms → read → restore clipboard
  - Clipboard retry: up to 5× with 20 ms delay on `OpenClipboard` contention
  - Return `string.Empty` if nothing captured within budget
  - Verify: select text in Notepad → trigger → confirm correct text returned

- [x] **`Services/OcrService.cs`**
  - `OcrEngine` created once from user profile languages (fallback: `en`)
  - `RecognizeCursorRegionAsync()`: capture 600×400 region centred on cursor
  - `RecognizeRegionAsync(Rectangle)`: capture exact screen rect
  - Both: `Graphics.CopyFromScreen` → PNG MemoryStream → `BitmapDecoder` → `SoftwareBitmap` → `OcrEngine.RecognizeAsync`
  - Concatenate `result.Lines` with space separator
  - Verify manually: point cursor at text on screen → trigger → confirm recognisable output

- [x] **`Services/QrCodeService.cs`**
  - `QrSettings(int TargetSizePx = 300, ECCLevel EccLevel = ECCLevel.Q)` record
  - Derive `PixelsPerModule = Max(1, Ceiling(TargetSizePx / moduleCount))`
  - `moduleCount = data.ModuleMatrix.Count` (from `QRCodeData` after generation)
  - `PngByteQRCode.GetGraphic(ppm)` → black on white
  - `BitmapImage` from `MemoryStream`, `.Freeze()` before returning
  - Verify: generate QR for "https://example.com" → image is non-null, scannable with phone

---

## Phase 3 — ViewModels

- [x] **`ViewModels/OverlayViewModel.cs`**
  - `INotifyPropertyChanged` (or `ObservableObject`)
  - Properties: `QrImage` (`BitmapSource?`), `SourceText` (`string`), `StatusText` (`string`), `StatusLevel` (`enum: None/Warning/Error`)
  - 150 ms debounce on `SourceText` setter: cancel pending timer, restart; on fire → call `QrCodeService.Generate` → update `QrImage` and `StatusText`
  - `StatusLevel` drives status bar visibility and color in XAML via converter

- [x] **`ViewModels/SettingsViewModel.cs`**
  - Load a deep copy of `AppSettings` on construction (working copy)
  - `ICommand Apply`: validate → `SettingsService.Save` → `HotkeyService` re-register → `SettingsService.ApplyAutostart`
  - `ICommand Cancel`: no-op (window closes without saving)
  - Hotkey recording state: `bool IsRecording`, `string HotkeyDisplay`

---

## Phase 4 — Windows / UI

- [x] **`RegionSelectorWindow.xaml` / `.cs`**
  - Fullscreen: `Left/Top/Width/Height` from `SystemParameters.VirtualScreen*`
  - `WindowStyle=None`, `AllowsTransparency=True`, `Topmost=True`
  - Background: `#66000000` (40% black overlay)
  - `Canvas` child: selection `Rectangle` with white 2 px border, `#22FFFFFF` fill
  - Mouse down → record start; mouse move → update rectangle; mouse up → set `TaskCompletionSource<Rectangle?>` result
  - `Esc` → set result `null`, close
  - Cursor: `Cursors.Cross`
  - Instruction text centered at bottom: `"Draw a region to read text from. Esc to cancel."`
  - `SelectAsync()` static factory method

- [x] **`OverlayWindow.xaml` / `.cs`**
  - `WindowStyle=None`, `AllowsTransparency=True`, acrylic/Mica backdrop
  - Header row (32 px): OCR Region button (left), Close `✕` button (right)
  - Content row: `TextBox` (50%) + QR `Image` (50%), both with 16 px padding
    - `TextBox`: monospace 13 px, `SpellCheck.IsEnabled=False`, byte-count caption below-right
    - `Image`: `Stretch=Uniform`, `BitmapScalingMode=NearestNeighbor`, 1 px `#E0E0E0` border
  - Status bar (32 px, full width): opacity-animated, amber/red background per `StatusLevel`
  - Positioning: 16 px right of cursor, clamped to monitor work area; flip left if needed
  - `Deactivated`: close (unless `RegionSelectorWindow` is the new foreground)
  - OCR button click: hide self → `RegionSelectorWindow.SelectAsync()` → if rect: OCR → sanitize → generate → update VM → show self; if null: just show self
  - `Esc` key binding → close

- [x] **`SettingsWindow.xaml` / `.cs`**
  - Standard `Window`, fixed 480 px width, modal
  - Sections: Hotkey, QR Code (size slider + ECC dropdown), Overlay (auto-dismiss), Startup (autostart checkbox), Symbol Filter (rule list)
  - Hotkey field: click → recording mode (accent tint bg, placeholder text); `KeyDown` → capture; `Esc` → cancel recording
  - Size slider: 200–600 step 50, live QR preview thumbnail to the right
  - ECC `ComboBox`: 4 items with recovery % in label; ⓘ tooltip
  - Symbol filter list: `DataGrid` or `ItemsControl` rows (match `TextBox`, replace `TextBox`, regex `CheckBox`, delete `Button`)
  - Footer: Cancel + Apply buttons (Apply is `IsDefault`)
  - Bind to `SettingsViewModel`

- [x] **`App.xaml.cs` — wire everything together**
  - `ShutdownMode = OnExplicitShutdown`
  - Instantiate all services; load settings; apply autostart
  - `NotifyIcon` with tray icon, tooltip, right-click menu (Settings, separator, Quit)
  - `HotkeyService.Register(...)` from loaded settings
  - `HotkeyService.HotkeyPressed`:
    - If overlay window is open → close it
    - Run pipeline: `SelectionService` → if empty: `OcrService.RecognizeCursorRegionAsync` → `TextSanitizerService.Sanitize` → `QrCodeService.Generate` → open new `OverlayWindow`
  - Handle `HotkeyService` registration failure → tray balloon notification
  - `Application.Exit`: `HotkeyService.Dispose()`, `NotifyIcon.Dispose()`

---

## Phase 5 — Build and Verify

- [x] **`dotnet build` with zero warnings**
  - Treat nullable warnings as errors during review
  - Fix all `CS8600`–`CS8625` nullability issues before proceeding

- [x] **`dotnet publish` self-contained EXE**
  ```
  dotnet publish src/QrApp -c Release -r win-x64 --self-contained true \
      -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
      -o publish/
  ```
  - Confirm output is a single `.exe`, ~60–80 MB

- [ ] **Manual smoke tests**
  - Select text in Notepad → `Ctrl+Shift+F2` → overlay shows with correct text and scannable QR
  - Select text in Chrome → hotkey → overlay shows
  - Hotkey while overlay is open → overlay closes and reopens with fresh capture
  - OCR button → draw region over text on screen → text appears in overlay
  - OCR button → press Esc → overlay restores previous text
  - Open Settings → change hotkey → Apply → new hotkey works
  - Open Settings → change QR size → QR preview updates → Apply → overlay reflects new size
  - Toggle autostart off → Apply → verify registry key removed; toggle on → key present
  - Corrupt `%APPDATA%\QrApp\settings.json` → restart app → settings reset to defaults
  - Long text (> 1 663 bytes) → warning appears at 80%; error above 100%

- [ ] **Clean-machine verification**
  - Copy `publish/QrApp.exe` to a Windows 11 VM with no .NET installed
  - Confirm app launches, tray icon appears, hotkey works
