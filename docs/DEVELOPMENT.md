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

App appears in the system tray. Select any text and press `Ctrl+Shift+F2`.

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
    <PackageReference Include="Markdig" Version="0.34.0" />
  </ItemGroup>

  <ItemGroup>
    <!-- Embed the Estonian user guide so HelpWindow can render it at runtime -->
    <EmbeddedResource Include="..\..\docs\JUHEND.md" LogicalName="JUHEND.md" />
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
dotnet add package Markdig --version 0.34.0
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
Services/AppSettings.cs
Services/SanitizerRule.cs
ViewModels/OverlayViewModel.cs
ViewModels/SettingsViewModel.cs
Helpers/NativeMethods.cs
Helpers/Converters.cs
OverlayWindow.xaml / .cs
RegionSelectorWindow.xaml / .cs
SettingsWindow.xaml / .cs
HelpWindow.xaml / .cs       ← renders embedded JUHEND.md via Markdig + WebBrowser
GlobalUsings.cs             ← resolves WPF vs WinForms type aliases
Assets/icon.ico             ← 256×256 (drawn; see tasks/todo.md Phase 1)
Assets/tray-icon.ico        ← 16×16 and 32×32 (drawn; see tasks/todo.md Phase 1)
```

NuGet packages required at runtime: `QRCoder`, `System.Drawing.Common`, `Microsoft.Xaml.Behaviors.Wpf`, `Markdig` (for `HelpWindow`).

---

## Implementation Order

Build in this sequence — each step is independently runnable:

1. **`NativeMethods.cs`** — P/Invoke signatures only; no logic.
2. **`HotkeyService.cs`** — Register hotkey on a message-only `HwndSource`; verify `WM_HOTKEY` fires.
3. **`SettingsService.cs`** + **`AppSettings.cs`** — Load/save JSON; verify corruption handling manually.
4. **`TextSanitizerService.cs`** — Pre-compiled regex rule engine with default rules; verify manually.
5. **`SelectionService.cs`** — Direct clipboard read with retry on `CLIPBRD_E_CANT_OPEN` (no input synthesis).
6. **`OcrService.cs`** — Manual region mode with optional upscale and line-break preservation; verify against on-screen text.
7. **`QrCodeService.cs`** — Generate QR; derive `PixelsPerModule` from `TargetSizePx`; verify output is scannable.
8. **`OverlayViewModel.cs`** — `QrImage`, `SourceText`, `StatusText`; wire 150 ms debounce.
9. **`RegionSelectorWindow.xaml`** — Fullscreen transparent canvas, mouse selection, returns `Rectangle?`.
10. **`HelpWindow.xaml`** — `WebBrowser` rendering embedded `JUHEND.md` via Markdig.
11. **`OverlayWindow.xaml`** — TextBox, QR image, draggable header bar with OCR / Help / Close buttons, status bar; wire to ViewModel.
12. **`SettingsViewModel.cs`** + **`SettingsWindow.xaml`** — Working copy, Apply/Cancel, all controls including OCR toggle switches.
13. **`App.xaml.cs`** — Compose everything; tray icon; `RunCapturePipelineAsync`; overlay-already-open handling.

---

## Key Implementation Details

### NativeMethods.cs

```csharp
using System.Runtime.InteropServices;

internal static class NativeMethods
{
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT lpPoint);
    [DllImport("user32.dll")] internal static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
    [DllImport("user32.dll")] internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    internal const int  WM_HOTKEY                = 0x0312;
    internal const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct RECT  { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
}
```

No `SendInput` / `INPUT` / `KEYBDINPUT` are needed — the app reads the clipboard directly. `MonitorFromPoint` + `GetMonitorInfo` are used by `OverlayWindow.PositionNearCursor` to centre the overlay on the active monitor's work area.

### HotkeyService.cs

Creates its own message-only window (`HWND_MESSAGE`, parent handle `-3`) — no main `Window` required.

```csharp
internal sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 9001;
    private HwndSource? _source;
    public event EventHandler? HotkeyPressed;

    public void Register(ModifierKeys modifiers, Key key)
    {
        Unregister();
        var parms = new HwndSourceParameters("QrApp_HotkeySource")
        {
            ParentWindow = new IntPtr(-3), // HWND_MESSAGE
            WindowStyle  = 0,
            Width = 0, Height = 0,
        };
        _source = new HwndSource(parms);
        _source.AddHook(WndProc);

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (!NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, (uint)modifiers, vk))
            throw new InvalidOperationException("The hotkey is already registered by another application.");
    }

    public void Unregister() { /* unhook + dispose _source */ }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose() => Unregister();
}
```

### SelectionService.cs

No input synthesis. The user copies with `Ctrl+C` first, then presses the hotkey — the service simply reads the clipboard, retrying briefly on the transient lock that can happen when another process (RDP clip-sync, antivirus, clipboard manager) holds it.

```csharp
internal sealed class SelectionService
{
    public string GetClipboardText()
    {
        // 8 × 25 ms ≈ 200 ms total — well under the user's perception of latency.
        for (int i = 0; i < 8; i++)
        {
            try { return Clipboard.ContainsText() ? Clipboard.GetText().Trim() : string.Empty; }
            catch (COMException) { }     // CLIPBRD_E_CANT_OPEN (0x800401D0)
            catch (ExternalException) { }
            System.Threading.Thread.Sleep(25);
        }
        return string.Empty;
    }
}
```

### OcrService.cs

Manual region only — no auto-cursor mode. Upscales the captured bitmap (bicubic, ≤ 3×, clamped to 4800 px max-dimension to stay under the Windows OCR engine's 5000 px input limit) when `OcrConfig.UpscaleEnabled` is set, dramatically improving recognition of small fonts.

```csharp
using Windows.Media.Ocr;
using Windows.Graphics.Imaging;

internal sealed class OcrService
{
    private readonly OcrEngine _engine =
        OcrEngine.TryCreateFromUserProfileLanguages() ??
        OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en"))!;

    public async Task<string> RecognizeRegionAsync(System.Drawing.Rectangle screenRect,
                                                   OcrConfig? config = null)
    {
        using var bmp = new System.Drawing.Bitmap(screenRect.Width, screenRect.Height);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
            g.CopyFromScreen(screenRect.Location, System.Drawing.Point.Empty, screenRect.Size);

        var toRecognize = (config?.UpscaleEnabled ?? true) ? Upscale(bmp) : bmp;
        try { return await RecognizeBitmapAsync(toRecognize, config?.PreserveLines ?? true); }
        finally { if (!ReferenceEquals(toRecognize, bmp)) toRecognize.Dispose(); }
    }

    private static System.Drawing.Bitmap Upscale(System.Drawing.Bitmap bmp)
    {
        int maxDim = Math.Max(bmp.Width, bmp.Height);
        int scale  = Math.Min(3, 4800 / Math.Max(1, maxDim));
        if (scale <= 1) return bmp;
        var scaled = new System.Drawing.Bitmap(bmp.Width * scale, bmp.Height * scale);
        using var g = System.Drawing.Graphics.FromImage(scaled);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(bmp, 0, 0, scaled.Width, scaled.Height);
        return scaled;
    }

    private async Task<string> RecognizeBitmapAsync(System.Drawing.Bitmap bmp, bool preserveLines)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Seek(0, SeekOrigin.Begin);

        var decoder = await BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
        var soft    = await decoder.GetSoftwareBitmapAsync();
        var result  = await _engine.RecognizeAsync(soft);
        var sep     = preserveLines ? "\n" : " ";
        return string.Join(sep, result.Lines.Select(l => l.Text));
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
| Empty selection | Confirm the user actually copied (`Ctrl+C`) before pressing the hotkey — the app reads the clipboard, it does not synthesise input. For apps where the user cannot copy (image viewers, terminals), enable Settings → Overlay → Show OCR Region button and use it as a manual fallback. |
| Overlay flickers | Ensure both `AllowsTransparency="True"` and `WindowStyle="None"` are set; `Background` must be non-null. |
| QR looks blurry | Set `RenderOptions.BitmapScalingMode="NearestNeighbor"` and `UseLayoutRounding="True"` on the overlay window. |
| Clipboard read fails persistently | Another process holds the clipboard (RDP clip-sync, antivirus, clipboard manager). The 8 × 25 ms retry handles transient locks; if the lock outlives the retry, the capture returns empty and the tray shows "Nothing to encode". |
| RegionSelector not covering all monitors | Check that `Left/Top/Width/Height` are set from `SystemParameters.VirtualScreen*` properties. |
| OCR returns empty | Confirm OCR upscale is enabled in Settings → OCR (default on). Also check region contrast and font size; `OcrEngine` cannot reliably detect very small or low-contrast text. |

---

## Code Style

- Nullable reference types enabled; no `!` suppressions without a comment explaining why.
- File-scoped namespaces (`namespace QrApp;`).
- Records for immutable data (`QrSettings`, `SanitizerRule`). Settings classes (`AppSettings`, `HotkeyConfig`, `QrCodeConfig`, `OverlayConfig`, `OcrConfig`, `SanitizerConfig`) are `sealed class` with public setters so `System.Text.Json` can populate them from `settings.json`.
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
