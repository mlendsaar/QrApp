# Development Guide

## Prerequisites

| Tool | Minimum version | Notes |
|---|---|---|
| Windows | 10 v1903 (build 18362) | Required for MSIX; Win 11 recommended for dev |
| .NET SDK | 8.0.x | `dotnet --version` to confirm |
| Visual Studio | 2022 17.8+ | Workload: **.NET desktop development** |
| Git | Any recent | — |

Visual Studio workload details — open **Visual Studio Installer** and ensure these are checked under **.NET desktop development**:
- .NET 8.0 Runtime
- Windows Presentation Foundation
- Windows App SDK (optional, for future WinUI 3 migration)

You can also build entirely from the CLI without Visual Studio.

---

## Getting Started

```bash
# 1. Clone
git clone https://github.com/mlendsaar/qrapp.git
cd QrApp

# 2. Restore packages
dotnet restore

# 3. Build
dotnet build -c Debug

# 4. Run
dotnet run --project src/QrApp
```

The app will appear in the system tray. Select any text on screen and press `Ctrl+Shift+Q`.

---

## Project Setup from Scratch

If starting fresh (no generated files yet), follow these steps:

### 1. Create the Solution and Projects

```bash
# Solution
dotnet new sln -n QrApp

# Main WPF app
dotnet new wpf -n QrApp -o src/QrApp --framework net8.0-windows
dotnet sln add src/QrApp/QrApp.csproj

# Unit tests
dotnet new xunit -n QrApp.Tests -o tests/QrApp.Tests --framework net8.0-windows
dotnet sln add tests/QrApp.Tests/QrApp.Tests.csproj
dotnet add tests/QrApp.Tests/QrApp.Tests.csproj reference src/QrApp/QrApp.csproj
```

### 2. Edit the .csproj

Open `src/QrApp/QrApp.csproj` and configure it:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <TargetPlatformVersion>10.0.19041.0</TargetPlatformVersion>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ApplicationIcon>Assets\icon.ico</ApplicationIcon>
    <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
    <Optimize>true</Optimize>
    <!-- Self-contained single-file: no .NET runtime required on target machine -->
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="QRCoder" Version="1.6.0" />
    <PackageReference Include="System.Drawing.Common" Version="8.0.0" />
    <PackageReference Include="Microsoft.Windows.SDK.Contracts" Version="10.0.19041.1" />
    <PackageReference Include="Microsoft.Xaml.Behaviors.Wpf" Version="1.1.77" />
  </ItemGroup>
</Project>
```

### 3. Install NuGet Packages

```bash
cd src/QrApp
dotnet add package QRCoder --version 1.6.0
dotnet add package System.Drawing.Common --version 8.0.0
dotnet add package Microsoft.Windows.SDK.Contracts --version 10.0.19041.1
dotnet add package Microsoft.Xaml.Behaviors.Wpf --version 1.1.77

cd ../../tests/QrApp.Tests
dotnet add package Microsoft.NET.Test.Sdk
dotnet add package xunit
dotnet add package xunit.runner.visualstudio
dotnet add package Moq
```

### 4. Create the Folder Structure

```bash
cd src/QrApp
mkdir Services ViewModels Helpers Assets
```

Create the following empty files to match the planned structure:

```
Services/HotkeyService.cs
Services/SelectionService.cs
Services/OcrService.cs
Services/TextSanitizerService.cs
Services/QrCodeService.cs
Services/SettingsService.cs
ViewModels/OverlayViewModel.cs
ViewModels/SettingsViewModel.cs
Helpers/NativeMethods.cs
OverlayWindow.xaml
OverlayWindow.xaml.cs
SettingsWindow.xaml
SettingsWindow.xaml.cs
Assets/icon.ico          ← add a 256×256 icon here
Assets/tray-icon.ico     ← 16×16 / 32×32 tray icon
```

---

## Implementation Order

Implement in this order to keep each step buildable and testable:

1. **`NativeMethods.cs`** — P/Invoke stubs (`SendInput`, `RegisterHotKey`, `GetCursorPos`).
2. **`HotkeyService.cs`** — Register a hotkey; log to debug output when pressed.
3. **`SelectionService.cs`** — Clipboard capture logic; unit test with a mock clipboard.
4. **`TextSanitizerService.cs`** — Strip/replace rules from config; unit test each default rule.
5. **`QrCodeService.cs`** — Generate QR from a hardcoded string; derive `PixelsPerModule` from `TargetSizePx`; unit test output dimensions.
6. **`OcrService.cs`** — `Windows.Media.Ocr` screenshot capture; integration test manually (requires a real screen).
7. **`OverlayViewModel.cs`** — Expose `QrImage`, `SourceText`, `StatusText` with `INotifyPropertyChanged`; wire 150 ms debounce.
8. **`OverlayWindow.xaml`** — Borderless window, editable `TextBox`, `Image` binding, buttons.
9. **`SettingsService.cs`** — Load/save JSON; hook into App startup/shutdown.
10. **`SettingsViewModel.cs`** + **`SettingsWindow.xaml`** — Working copy pattern; Apply/Cancel; press-to-record hotkey field; size slider; color pickers; rule list.
11. **`App.xaml.cs`** — Wire everything together; tray icon; Settings → Quit menu.

---

## Key Implementation Details

### NativeMethods.cs — complete P/Invoke surface

```csharp
using System.Runtime.InteropServices;

internal static class NativeMethods
{
    // Hotkey
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Input synthesis
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    // Mouse position
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT lpPoint);

    // Screen info
    [DllImport("user32.dll")] internal static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
    [DllImport("user32.dll")] internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    internal const uint MONITOR_DEFAULTTONEAREST = 2;
    internal const int MOD_CONTROL = 0x0002;
    internal const int MOD_SHIFT   = 0x0004;
    internal const int WM_HOTKEY   = 0x0312;
    internal const int VK_C        = 0x43;
    internal const uint INPUT_KEYBOARD = 1;
    internal const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)] internal struct RECT
    { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] internal struct MONITORINFO
    {
        public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)] internal struct KEYBDINPUT
    { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Explicit)] internal struct INPUT
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(4)] public KEYBDINPUT ki;
    }
}
```

### HotkeyService.cs — skeleton

```csharp
using System.Windows.Interop;

internal sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 9001;
    private HwndSource? _source;

    public event EventHandler? HotkeyPressed;

    public void Register(System.Windows.Input.ModifierKeys modifiers, System.Windows.Input.Key key)
    {
        var helper = new WindowInteropHelper(Application.Current.MainWindow!);
        _source = HwndSource.FromHwnd(helper.EnsureHandle());
        _source.AddHook(WndProc);
        uint mod = (uint)modifiers;
        uint vk  = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (!NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, mod, vk))
            throw new InvalidOperationException("Hotkey already registered by another application.");
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

    public void Dispose()
    {
        if (_source is not null)
        {
            NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
            _source.RemoveHook(WndProc);
        }
    }
}
```

### SelectionService.cs — clipboard capture

```csharp
internal sealed class SelectionService
{
    private const int MaxRetries = 5;
    private const int RetryDelayMs = 20;

    public async Task<string> GetSelectedTextAsync()
    {
        // 1. Save clipboard
        IDataObject? previous = null;
        try { previous = Clipboard.GetDataObject(); } catch { }

        // 2. Clear and synthesise Ctrl+C
        Clipboard.Clear();
        SendCtrlC();

        // 3. Poll for text (300 ms budget)
        string result = string.Empty;
        var deadline = DateTime.UtcNow.AddMilliseconds(300);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(30);
            if (Clipboard.ContainsText()) { result = Clipboard.GetText(); break; }
        }

        // 4. Restore clipboard
        try { if (previous is not null) Clipboard.SetDataObject(previous, true); }
        catch { }

        return result.Trim();
    }

    private static void SendCtrlC()
    {
        var inputs = new NativeMethods.INPUT[4];
        // Ctrl down
        inputs[0].type = NativeMethods.INPUT_KEYBOARD;
        inputs[0].ki.wVk = 0x11; // VK_CONTROL
        // C down
        inputs[1].type = NativeMethods.INPUT_KEYBOARD;
        inputs[1].ki.wVk = (ushort)NativeMethods.VK_C;
        // C up
        inputs[2].type = NativeMethods.INPUT_KEYBOARD;
        inputs[2].ki.wVk = (ushort)NativeMethods.VK_C;
        inputs[2].ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;
        // Ctrl up
        inputs[3].type = NativeMethods.INPUT_KEYBOARD;
        inputs[3].ki.wVk = 0x11;
        inputs[3].ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
```

### QrCodeService.cs — QR generation

```csharp
using QRCoder;
using System.Windows.Media.Imaging;

internal sealed class QrCodeService
{
    public BitmapSource Generate(string text, int pixelsPerModule = 10,
                                  QRCodeGenerator.ECCLevel ecc = QRCodeGenerator.ECCLevel.Q)
    {
        using var generator = new QRCodeGenerator();
        using var data      = generator.CreateQrCode(text, ecc);
        using var code      = new PngByteQRCode(data);
        byte[] png = code.GetGraphic(pixelsPerModule);

        var image = new BitmapImage();
        using var ms = new System.IO.MemoryStream(png);
        image.BeginInit();
        image.CacheOption  = BitmapCacheOption.OnLoad;
        image.StreamSource = ms;
        image.EndInit();
        image.Freeze(); // make cross-thread safe
        return image;
    }
}
```

---

## Running Tests

```bash
dotnet test tests/QrApp.Tests --logger "console;verbosity=normal"
```

Tests must not depend on a real screen or clipboard — mock `SelectionService` via an interface.

---

## Building for Release

The app is **always published self-contained** — no .NET runtime required on the target machine. This is a hard project requirement.

### Self-Contained Single-File EXE (standard release)

```bash
dotnet publish src/QrApp -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -o publish/
```

Output: `publish/QrApp.exe` — ~60–80 MB, runs on any Windows 10 1903+ or Windows 11 machine with no prerequisites.

### MSIX Package (recommended for distribution)

1. Open the solution in Visual Studio.
2. Add a **Windows Application Packaging Project** (`packaging/QrApp.wapproj`), referencing `src/QrApp`.
3. In the packaging project, set the package identity, publisher, and version.
4. Right-click the packaging project → **Publish** → **Create App Packages**.
5. Output: `packaging/AppPackages/QrApp_1.0.0.0_x64.msix`.

For CI/CD (GitHub Actions), add a workflow step:

```yaml
- name: Build MSIX
  run: |
    msbuild packaging/QrApp.wapproj /p:Configuration=Release /p:Platform=x64 \
      /p:AppxBundlePlatforms="x64" /p:AppxPackageDir=AppPackages/ /p:AppxBundle=Always
```

---

## Debugging Tips

| Problem | Solution |
|---|---|
| Hotkey not firing | Check if another app owns the shortcut (`Spy++` or `nirsoft HotkeysList`). Change hotkey in settings. |
| Empty selection always | Test in Notepad first. Apps with custom copy behaviour (some terminals, Electron apps) may not respond to synthesised `Ctrl+C`. |
| Overlay flickers | Ensure `AllowsTransparency="True"` and `WindowStyle="None"` are both set; set `Background` to a non-null brush. |
| Clipboard restore fails | Clipboard restore is best-effort; some data object types (e.g. `OLE` objects) cannot survive a round-trip. Log and swallow the exception. |
| High DPI scaling | Use `UseLayoutRounding="True"` on the overlay `Window` and `SnapsToDevicePixels="True"` on the `Image` to avoid blurry QR rendering. |

---

## Code Style

- **Nullable reference types** enabled; no `!` suppressions without a comment.
- **File-scoped namespaces** (`namespace QrApp;`).
- **Records** for immutable data (`QrSettings`, `HotkeyConfig`).
- **`sealed`** on all concrete service classes.
- No external DI container — services are composed manually in `App.xaml.cs` (app is small enough).
- No comments except where a Win32 quirk or non-obvious invariant requires explanation.

---

## Git Workflow

```
main          — stable, tagged releases only
feature/*     — one branch per feature PR
claude/*      — AI-assisted work branches
```

Branch naming: `feature/tray-icon`, `fix/clipboard-restore`, etc.

Commit message format:
```
<type>: <short summary>

Types: feat | fix | refactor | test | docs | chore
```

Example: `feat: add auto-dismiss timer to overlay window`
