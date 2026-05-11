# QrApp

A lightweight .NET 8 Windows native application that instantly generates a QR code from whatever text is currently selected on screen.

## What It Does

Select any text anywhere on your desktop — a URL, phone number, address, Wi-Fi password, or any string — and QrApp captures it and displays a scannable QR code in a small overlay window. No copy-paste, no manual typing.

## How It Works

1. The user selects text with the mouse (or keyboard).
2. A global hotkey (default: `Ctrl+Shift+Q`) triggers the app.
3. QrApp reads the selected text from the clipboard (by temporarily sending `Ctrl+C` via the Windows SendInput API).
4. A QR code is generated and shown in a floating overlay window.
5. The overlay auto-dismisses when focus is lost, or the user presses `Esc`.

## Tech Stack

| Layer | Choice | Reason |
|---|---|---|
| Runtime | .NET 8 | LTS, best Windows integration |
| UI Framework | WPF | Mature, lightweight, excellent Windows look-and-feel |
| QR Generation | [QRCoder](https://github.com/codebude/QRCoder) | MIT, no native deps, fast |
| Clipboard/Input | Windows API via P/Invoke | Required for reading active selection |
| Hotkey registration | Windows `RegisterHotKey` API | System-wide hotkey, no elevated rights needed |
| Packaging | MSIX (single-file optional) | Clean install/uninstall, Start Menu entry |

## Project Structure

```
QrApp/
├── src/
│   └── QrApp/
│       ├── QrApp.csproj          # WPF, net8.0-windows, nullable enabled
│       ├── App.xaml / App.xaml.cs
│       ├── MainWindow.xaml       # Hidden host window (lives in system tray)
│       ├── MainWindow.xaml.cs
│       ├── OverlayWindow.xaml    # Floating QR display
│       ├── OverlayWindow.xaml.cs
│       ├── Services/
│       │   ├── HotkeyService.cs       # Registers/unregisters global hotkey
│       │   ├── SelectionService.cs    # Captures selected text via clipboard
│       │   └── QrCodeService.cs       # Wraps QRCoder, returns BitmapSource
│       ├── ViewModels/
│       │   └── OverlayViewModel.cs    # Bindable QR image + status text
│       ├── Helpers/
│       │   └── NativeMethods.cs       # P/Invoke signatures
│       └── Assets/
│           ├── icon.ico
│           └── tray-icon.ico
├── tests/
│   └── QrApp.Tests/
│       ├── QrApp.Tests.csproj
│       ├── QrCodeServiceTests.cs
│       └── SelectionServiceTests.cs
├── packaging/
│   └── QrApp.wapproj             # Windows Application Packaging project (MSIX)
├── docs/
│   ├── ARCHITECTURE.md
│   └── DEVELOPMENT.md
├── .gitignore
└── QrApp.sln
```

## Feature Scope (v1.0)

- [x] Global hotkey capture (`Ctrl+Shift+Q`, user-configurable)
- [x] Read selected text via SendInput → clipboard → restore
- [x] Generate QR code up to version 40 (up to ~4 KB of text)
- [x] Display overlay near mouse cursor, auto-position to stay on screen
- [x] Copy QR image to clipboard from overlay (click or keyboard)
- [x] Save QR image as PNG via overlay button
- [x] System tray icon with settings and quit
- [x] Persist settings (hotkey, QR colors, overlay timeout) to `%APPDATA%\QrApp\settings.json`

## Out of Scope (v1.0)

- OCR / image-to-QR
- QR code reading / decoding
- Non-Windows platforms
- URL shortening before encoding

## Prerequisites

- Windows 10 version 1903+ or Windows 11
- .NET 8 Desktop Runtime (bundled in MSIX, optional in self-contained publish)
- Visual Studio 2022 17.8+ **or** .NET 8 SDK CLI

## Quick Start

```bash
git clone https://github.com/mlendsaar/qrapp.git
cd QrApp
dotnet restore
dotnet run --project src/QrApp
```

Press `Ctrl+Shift+Q` with any text selected to see the overlay.

See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for full build, test, and publish instructions.

## License

MIT
