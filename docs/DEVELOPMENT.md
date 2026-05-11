# Development Guide

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| Windows | 11 (22000+) | Target OS; also fine for dev |
| .NET SDK | 8.0.x | `dotnet --version` to confirm |
| Visual Studio | 2022 17.8+ | Workload: **.NET desktop development** |
| Git | Any | — |

---

## Getting Started

```bash
git clone https://github.com/mlendsaar/qrapp.git
cd QrApp
dotnet restore
dotnet build -c Debug
dotnet run --project src/QrApp
```

App appears in the system tray. Select any text and press `Ctrl+Shift+Q`.

---

## Project Setup from Scratch

### 1. Create Solution and Projects

```bash
dotnet new sln -n QrApp
dotnet new wpf -n QrApp -o src/QrApp --framework net8.0-windows
dotnet sln add src/QrApp/QrApp.csproj
```

### 2. Configure the .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <!-- Windows 11 TFM — enables Windows.Media.Ocr without extra packages -->
    <TargetFramework>net8.0-windows10.0.22000.0</TargetFramework>
    <SupportedOSPlatformVersion>10.0.22000.0</SupportedOSPlatformVersion>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ApplicationIcon>Assets\icon.ico</ApplicationIcon>
    <AllowUnsafeBlocks>false</AllowUnsafeBlocks>
    <!-- Self-contained single-file is the only supported publish mode -->
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="QRCoder" Version="1.6.0" />
    <PackageReference Include="System.Drawing.Common" Version="8.0.0" />
    <PackageReference Include="Microsoft.Xaml.Behaviors.Wpf" Version="1.1.77" />
  </ItemGroup>
</Project>
```

> `Windows.Media.Ocr` is part of the Windows 11 SDK and accessed directly through the `net8.0-windows10.0.22000.0` TFM — no extra NuGet package needed.

### 3. Install NuGet Packages

```bash
cd src/QrApp
dotnet add package QRCoder --version 1.6.0
dotnet add package System.Drawing.Common --version 8.0.0
dotnet add package Microsoft.Xaml.Behaviors.Wpf --version 1.1.77
```

### 4. Create the Folder Structure

```bash
cd src/QrApp
mkdir Services ViewModels Helpers Assets
```

Files to create:

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
OverlayWindow.xaml / .cs
RegionSelectorWindow.xaml / .cs
SettingsWindow.xaml / .cs
Assets/icon.ico            ← 256×256 (drawn; see tasks/todo.md Phase 1)
Assets/tray-icon.ico       ← 16×16 and 32×32 (drawn; see tasks/todo.md Phase 1)
```

---

## Implementation Order

Build in this sequence — each step is independently runnable:

1. **`NativeMethods.cs`** — P/Invoke signatures only; no logic.
2. **`HotkeyService.cs`** — Register hotkey; verify `WM_HOTKEY` fires in debug output.
3. **`SettingsService.cs`** — Load/save JSON; verify corruption handling manually.
4. **`TextSanitizerService.cs`** — Rule engine with default rules; verify manually.
5. **`SelectionService.cs`** — Clipboard capture via `SendInput`.
6. **`OcrService.cs`** — Both modes (cursor region + explicit rect); verify manually against on-screen text.
7. **`QrCodeService.cs`** — Generate QR; derive `PixelsPerModule` from `TargetSizePx`; verify output is scannable.
8. **`OverlayViewModel.cs`** — `QrImage`, `SourceText`, `StatusText`; wire 150 ms debounce.
9. **`RegionSelectorWindow.xaml`** — Fullscreen transparent canvas, mouse selection, returns `Rectangle?`.
10. **`OverlayWindow.xaml`** — TextBox, QR image, OCR button, status bar; wire to ViewModel.
11. **`SettingsViewModel.cs`** + **`SettingsWindow.xaml`** — Working copy, Apply/Cancel, all controls.
12. **`App.xaml.cs`** — Compose everything; tray icon; overlay-already-open handling.

---

## Key Implementation Details

### NativeMethods.cs

```csharp
using System.Runtime.InteropServices;

internal static class NativeMethods
{
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll", SetLastError = true)] internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] internal static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
    [DllImport("user32.dll")] internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    internal const int  WM_HOTKEY            = 0x0312;
    internal const int  MOD_CONTROL          = 0x0002;
    internal const int  MOD_SHIFT            = 0x0004;
    internal const int  VK_C                 = 0x43;
    internal const uint INPUT_KEYBOARD       = 1;
    internal const uint KEYEVENTF_KEYUP      = 0x0002;
    internal const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)] internal struct POINT  { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct RECT   { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)]
    internal struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(4)] public KEYBDINPUT ki; }
}
```

### HotkeyService.cs

```csharp
internal sealed class HotkeyService : IDisposable
{
    private const int Id = 9001;
    private HwndSource? _source;
    public event EventHandler? HotkeyPressed;

    public void Register(ModifierKeys modifiers, Key key)
    {
        _source = HwndSource.FromHwnd(new WindowInteropHelper(Application.Current.MainWindow!).EnsureHandle());
        _source.AddHook(WndProc);
        if (!NativeMethods.RegisterHotKey(_source.Handle, Id, (uint)modifiers, (uint)KeyInterop.VirtualKeyFromKey(key)))
            throw new InvalidOperationException("Hotkey is already registered by another application.");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == Id)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source is not null) NativeMethods.UnregisterHotKey(_source.Handle, Id);
    }
}
```

### SelectionService.cs

```csharp
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
            if (Clipboard.ContainsText()) { result = Clipboard.GetText(); break; }
        }

        try { if (saved is not null) Clipboard.SetDataObject(saved, true); } catch { }
        return result.Trim();
    }

    private static void SendCtrlC()
    {
        var inputs = new NativeMethods.INPUT[4];
        inputs[0].type = NativeMethods.INPUT_KEYBOARD; inputs[0].ki.wVk = 0x11;           // Ctrl down
        inputs[1].type = NativeMethods.INPUT_KEYBOARD; inputs[1].ki.wVk = NativeMethods.VK_C; // C down
        inputs[2].type = NativeMethods.INPUT_KEYBOARD; inputs[2].ki.wVk = NativeMethods.VK_C;
        inputs[2].ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;                                  // C up
        inputs[3].type = NativeMethods.INPUT_KEYBOARD; inputs[3].ki.wVk = 0x11;
        inputs[3].ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;                                  // Ctrl up
        NativeMethods.SendInput(4, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
```

### OcrService.cs

```csharp
using Windows.Media.Ocr;
using Windows.Graphics.Imaging;

internal sealed class OcrService
{
    private readonly OcrEngine _engine =
        OcrEngine.TryCreateFromUserProfileLanguages() ??
        OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en"));

    public async Task<string> RecognizeCursorRegionAsync()
    {
        NativeMethods.GetCursorPos(out var pt);
        var region = new System.Drawing.Rectangle(pt.X - 300, pt.Y - 200, 600, 400);
        return await RecognizeRegionAsync(region);
    }

    public async Task<string> RecognizeRegionAsync(System.Drawing.Rectangle screenRect)
    {
        using var bmp = new System.Drawing.Bitmap(screenRect.Width, screenRect.Height);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
            g.CopyFromScreen(screenRect.Location, System.Drawing.Point.Empty, screenRect.Size);

        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Seek(0, SeekOrigin.Begin);

        var decoder = await BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
        var soft    = await decoder.GetSoftwareBitmapAsync();
        var result  = await _engine.RecognizeAsync(soft);
        return string.Join(" ", result.Lines.Select(l => l.Text));
    }
}
```

### TextSanitizerService.cs

```csharp
internal sealed class TextSanitizerService
{
    private readonly IReadOnlyList<(string match, string replace, Regex? regex)> _rules;

    public TextSanitizerService(IEnumerable<SanitizerRule> rules)
    {
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
                : input.Replace(match, replace);
        return input.Trim();
    }
}
```

### QrCodeService.cs

```csharp
using QRCoder;

internal sealed class QrCodeService
{
    public BitmapSource Generate(string text, QrSettings settings)
    {
        using var generator = new QRCodeGenerator();
        using var data      = generator.CreateQrCode(text, settings.EccLevel);
        int moduleCount     = data.ModuleMatrix.Count;
        int ppm             = Math.Max(1, (int)Math.Ceiling((double)settings.TargetSizePx / moduleCount));
        using var code      = new PngByteQRCode(data);
        byte[] png          = code.GetGraphic(ppm);   // black on white

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

### SettingsService.cs

```csharp
internal sealed class SettingsService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QrApp", "settings.json");

    public AppSettings Load()
    {
        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? AppSettings.Default;
        }
        catch   // missing file, locked file, or malformed JSON → reset
        {
            Save(AppSettings.Default);
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath,
            JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void ApplyAutostart(bool enable)
    {
        const string regKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(regKey, writable: true);
        if (enable) key?.SetValue("QrApp", Environment.ProcessPath!);
        else        key?.DeleteValue("QrApp", throwOnMissingValue: false);
    }
}
```

### RegionSelectorWindow.xaml.cs — key pattern

```csharp
internal sealed partial class RegionSelectorWindow : Window
{
    private readonly TaskCompletionSource<System.Drawing.Rectangle?> _tcs;
    private System.Windows.Point _start;

    public RegionSelectorWindow(TaskCompletionSource<System.Drawing.Rectangle?> tcs)
    {
        _tcs = tcs;
        InitializeComponent();
        // Cover all monitors
        Left   = SystemParameters.VirtualScreenLeft;
        Top    = SystemParameters.VirtualScreenTop;
        Width  = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    public static Task<System.Drawing.Rectangle?> SelectAsync()
    {
        var tcs = new TaskCompletionSource<System.Drawing.Rectangle?>();
        new RegionSelectorWindow(tcs).Show();
        return tcs.Task;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { _tcs.SetResult(null); Close(); }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)  => _start = e.GetPosition(this);
    protected override void OnMouseMove(MouseButtonEventArgs e)  => UpdateSelectionRect(e.GetPosition(this));
    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        var end  = e.GetPosition(this);
        var rect = new System.Drawing.Rectangle(
            (int)Math.Min(_start.X, end.X) + (int)Left,
            (int)Math.Min(_start.Y, end.Y) + (int)Top,
            (int)Math.Abs(end.X - _start.X),
            (int)Math.Abs(end.Y - _start.Y));
        _tcs.SetResult(rect.Width > 4 && rect.Height > 4 ? rect : null);
        Close();
    }
}
```

---

## Building for Release

```bash
dotnet publish src/QrApp -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -o publish/
```

Output: `publish/QrApp.exe` (~70 MB). Runs on any Windows 11 machine with no prerequisites. Distribute this file directly.

---

## Debugging Tips

| Problem | Solution |
|---|---|
| Hotkey not firing | Use `nirsoft HotkeysList` to check conflicts. Change hotkey in Settings. |
| Empty selection | Test in Notepad first. Some apps (Electron, terminals) ignore synthesised `Ctrl+C` — OCR fallback should kick in automatically. |
| Overlay flickers | Ensure both `AllowsTransparency="True"` and `WindowStyle="None"` are set; `Background` must be non-null. |
| QR looks blurry | Set `RenderOptions.BitmapScalingMode="NearestNeighbor"` and `UseLayoutRounding="True"` on the overlay window. |
| Clipboard restore fails | `Clipboard.SetDataObject` with OLE objects is best-effort; catch and ignore. |
| RegionSelector not covering all monitors | Check that `Left/Top/Width/Height` are set from `SystemParameters.VirtualScreen*` properties. |
| OCR returns empty | Ensure the region has sufficient contrast and text size; `OcrEngine` may not detect very small fonts. |

---

## Code Style

- Nullable reference types enabled; no `!` suppressions without a comment explaining why.
- File-scoped namespaces (`namespace QrApp;`).
- Records for immutable data (`QrSettings`, `AppSettings`, `SanitizerRule`, `HotkeyConfig`).
- `sealed` on all concrete service and window classes.
- Services composed manually in `App.xaml.cs` — no DI container.
- Comments only for non-obvious Win32 behaviour or invariants.

---

## Git Workflow

```
main        — stable releases only
feature/*   — one branch per feature PR
claude/*    — AI-assisted branches
```

Commit format: `<type>: <summary>` — types: `feat | fix | refactor | test | docs | chore`
