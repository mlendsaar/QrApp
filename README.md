# QrApp

A lightweight .NET 8 Windows 11 application that instantly generates a QR code from whatever text is currently selected on screen.

## What It Does

Select any text anywhere on your desktop — a URL, phone number, address, Wi-Fi password, or any string — trigger the hotkey, and QrApp shows a floating overlay with the captured text in an editable box and the generated QR code alongside it. No copy-paste, no manual typing.

If the text can't be selected normally (e.g. inside an image or a locked PDF), click the OCR button in the overlay to draw a region on screen — just like Snipping Tool — and QrApp reads the text from that area automatically.

## How It Works

1. Select text anywhere on screen.
2. Press the global hotkey (`Ctrl+Shift+Q` by default).
3. QrApp captures the selection, sanitizes it, generates the QR code, and opens the overlay.
4. Edit the text in the overlay if needed — the QR updates live.
5. Press `Esc` or click away to close. If the hotkey is pressed while the overlay is already open, it closes and a fresh one opens.

## Tech Stack

| Layer | Choice |
|---|---|
| Runtime | .NET 8 LTS (`net8.0-windows10.0.22000.0`) |
| UI | WPF |
| QR generation | [QRCoder](https://github.com/codebude/QRCoder) |
| Screen OCR | `Windows.Media.Ocr` (built into Windows 11) |
| Global hotkey / input | Win32 API via P/Invoke |
| Settings | `System.Text.Json` → `%APPDATA%\QrApp\settings.json` |
| Distribution | Self-contained single-file EXE |

## Project Structure

```
QrApp/
├── src/
│   └── QrApp/
│       ├── QrApp.csproj
│       ├── App.xaml / App.xaml.cs
│       ├── MainWindow.xaml / .cs          # Hidden host (system tray)
│       ├── OverlayWindow.xaml / .cs       # Floating QR display + text editor
│       ├── RegionSelectorWindow.xaml / .cs # Fullscreen snip-style OCR selector
│       ├── SettingsWindow.xaml / .cs      # Settings dialog
│       ├── Services/
│       │   ├── HotkeyService.cs
│       │   ├── SelectionService.cs
│       │   ├── OcrService.cs
│       │   ├── TextSanitizerService.cs
│       │   ├── QrCodeService.cs
│       │   └── SettingsService.cs
│       ├── ViewModels/
│       │   ├── OverlayViewModel.cs
│       │   └── SettingsViewModel.cs
│       ├── Helpers/
│       │   └── NativeMethods.cs
│       └── Assets/
│           ├── icon.ico
│           └── tray-icon.ico
├── tests/
│   └── QrApp.Tests/
│       ├── QrCodeServiceTests.cs
│       ├── SelectionServiceTests.cs
│       └── TextSanitizerServiceTests.cs
├── packaging/
│   └── QrApp.wapproj
├── docs/
│   ├── ARCHITECTURE.md
│   ├── DEVELOPMENT.md
│   └── UI.md
├── .gitignore
└── QrApp.sln
```

## Functional Requirements

### Text Capture
- Capture selected text via `SendInput` → clipboard → restore on hotkey press
- Fall back to `Windows.Media.Ocr` (cursor-region screenshot) when clipboard capture returns empty
- Restore clipboard after every capture attempt

### Overlay
- Open near the mouse cursor, clamped to the current monitor
- Show captured text in an editable `TextBox` alongside the QR code
- Regenerate QR in real time as the user edits (150 ms debounce)
- **OCR button**: hides overlay, shows `RegionSelectorWindow` for user to draw a screen region; OCRs that region and populates the text box
- Status bar: warning at 80–100% QR capacity; error above 100% ("Too much data — edit the text to reduce it.")
- Dismiss on focus loss or `Esc`; optional auto-dismiss timer
- If hotkey pressed while overlay is open: close the current overlay and open a fresh one

### QR Generation
- Auto-select QR version 1–40 based on text and ECC level
- Default ECC Q (25% damage recovery); user-configurable
- Target size 200–600 px (square); `PixelsPerModule` derived from actual module count
- Output always black on white

### Symbol Filtering
- **Permanent**: configurable strip/replace rules applied on every capture (default rules strip BOM, zero-width spaces, soft hyphens, null bytes; normalise line endings)
- **Temporary**: editable `TextBox` in overlay for per-capture adjustments

### Settings
- Hotkey (press-to-record)
- QR target size slider (200–600 px)
- ECC level dropdown (L / M / Q / H)
- Overlay auto-dismiss toggle + seconds
- Launch at Windows startup toggle (default: on)
- Symbol filter rule list (match, replacement, regex toggle, add/delete)
- Apply saves immediately; Cancel discards changes

### System Tray
- Icon always present; right-click → Settings / Quit

## Non-Functional Requirements

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#non-functional-requirements) for the full NFR table. Summary:

- **OS**: Windows 11 only
- **SDK**: .NET 8 LTS
- **Distribution**: self-contained EXE, manual
- **Autostart**: on by default (registry)
- **Internet**: not required
- **Settings corruption**: auto-reset to defaults

## Prerequisites (build machine)

- Windows 11
- .NET 8 SDK (`dotnet --version`)
- Visual Studio 2022 17.8+ or CLI only

No prerequisites on the target machine — the EXE is self-contained.

## Quick Start

```bash
git clone https://github.com/mlendsaar/qrapp.git
cd QrApp
dotnet restore
dotnet run --project src/QrApp
```

Press `Ctrl+Shift+Q` with any text selected to see the overlay.

## Documentation

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — component design, data flow, NFRs, performance budget, acceptance criteria
- [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) — build, publish, code skeletons, implementation order
- [docs/UI.md](docs/UI.md) — layout, color palette, typography, interaction design

## License

MIT
