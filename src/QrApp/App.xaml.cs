using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;

namespace QrApp;

public sealed partial class App : System.Windows.Application
{
    private readonly SettingsService      _settingsService  = new();
    private readonly HotkeyService        _hotkeyService    = new();
    private readonly SelectionService     _selectionService = new();
    private readonly OcrService           _ocrService       = new();
    private readonly QrCodeService        _qrService        = new();

    private TextSanitizerService _sanitizerService = null!;
    private AppSettings          _settings         = null!;
    private NotifyIcon           _trayIcon         = null!;
    private OverlayWindow?       _overlay;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _settings         = _settingsService.Load();
        _sanitizerService = new TextSanitizerService(_settings.Sanitizer.Rules);

        _settingsService.ApplyAutostart(_settings.Autostart);
        InitTray();
        RegisterHotkey();
    }

    private void InitTray()
    {
        Icon icon;
        try
        {
            var info = GetResourceStream(new Uri("pack://application:,,,/Assets/tray-icon.ico"));
            icon = new Icon(info!.Stream);
        }
        catch { icon = SystemIcons.Application; }

        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit",     null, (_, _) => Quit());

        _trayIcon = new NotifyIcon
        {
            Icon             = icon,
            Text             = "QrApp — Ctrl+Shift+F2 to capture",
            Visible          = true,
            ContextMenuStrip = menu,
        };
    }

    private void RegisterHotkey()
    {
        try
        {
            _hotkeyService.Register(_settings.Hotkey.ParseModifiers(), _settings.Hotkey.ParseKey());
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        }
        catch (InvalidOperationException)
        {
            _trayIcon.ShowBalloonTip(3000, "QrApp", "Hotkey in use — change it in Settings.", ToolTipIcon.Warning);
        }
    }

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        if (_overlay is not null)
        {
            _overlay.Close();
            _overlay = null;
        }

        await RunCapturePipelineAsync();
    }

    private async Task RunCapturePipelineAsync()
    {
        string raw;
        try { raw = _selectionService.GetClipboardText(); }
        catch { raw = string.Empty; }

        var text = _sanitizerService.Sanitize(raw);

        if (string.IsNullOrWhiteSpace(text))
        {
            _trayIcon.ShowBalloonTip(2000, "QrApp", "Nothing to encode.", ToolTipIcon.Info);
            return;
        }

        var vm = new OverlayViewModel(_qrService, _settings.Qr.ToQrSettings())
        {
            HotkeyLabel = _settings.Hotkey.ToLabel(),
        };
        _overlay = new OverlayWindow(vm, _ocrService, _sanitizerService, _settings.Overlay.ShowOcrButton, _settings.Ocr);
        _overlay.PositionNearCursor();
        _overlay.Closed += (_, _) => _overlay = null;
        _overlay.SetInitialText(text);
        _overlay.Show();
        _overlay.Activate();
    }

    private void OpenSettings()
    {
        var vm  = new SettingsViewModel(_settings, _settingsService, _hotkeyService);
        var win = new SettingsWindow(vm, _qrService);
        win.ShowDialog();

        // Re-read settings in case they changed (Apply was clicked)
        _settings         = _settingsService.Load();
        _sanitizerService = new TextSanitizerService(_settings.Sanitizer.Rules);
    }

    private void Quit()
    {
        _overlay?.Close();
        _hotkeyService.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
