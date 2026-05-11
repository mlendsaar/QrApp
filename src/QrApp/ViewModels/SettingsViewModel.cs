using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using QRCoder;

namespace QrApp;

internal sealed class SettingsViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly SettingsService _settingsService;
    private readonly HotkeyService   _hotkeyService;

    // Working copy — persisted only on Apply
    private AppSettings _working;

    private bool _isRecordingHotkey;
    private string _hotkeyDisplay = string.Empty;
    private string? _hotkeyConflictMessage;

    public SettingsViewModel(AppSettings current, SettingsService settingsService, HotkeyService hotkeyService)
    {
        _settingsService = settingsService;
        _hotkeyService   = hotkeyService;

        // Deep-copy the current settings into the working copy
        _working = DeepCopy(current);
        RefreshHotkeyDisplay();

        // Build observable rule list from working copy
        Rules = new ObservableCollection<SanitizerRuleViewModel>(
            _working.Sanitizer.Rules.Select(r => new SanitizerRuleViewModel(r)));
        Rules.CollectionChanged += (_, _) => SyncRulesToWorking();

        ApplyCommand  = new RelayCommand(Apply);
        CancelCommand = new RelayCommand(Cancel);
        AddRuleCommand = new RelayCommand(AddRule);
    }

    // ── Hotkey ──────────────────────────────────────────────────────────────

    public bool IsRecordingHotkey
    {
        get => _isRecordingHotkey;
        set { _isRecordingHotkey = value; OnPropertyChanged(); OnPropertyChanged(nameof(HotkeyPlaceholder)); }
    }

    public string HotkeyDisplay
    {
        get => _hotkeyDisplay;
        private set { _hotkeyDisplay = value; OnPropertyChanged(); }
    }

    public string HotkeyPlaceholder =>
        IsRecordingHotkey ? "Press a key combination…" : _hotkeyDisplay;

    public string? HotkeyConflictMessage
    {
        get => _hotkeyConflictMessage;
        private set { _hotkeyConflictMessage = value; OnPropertyChanged(); }
    }

    public void StartRecording()  => IsRecordingHotkey = true;
    public void CancelRecording() => IsRecordingHotkey = false;

    public void RecordHotkey(ModifierKeys modifiers, Key key)
    {
        if (modifiers == ModifierKeys.None || key == Key.None) return;
        _working.Hotkey.Modifiers = modifiers.ToString();
        _working.Hotkey.Key       = key.ToString();
        RefreshHotkeyDisplay();
        IsRecordingHotkey     = false;
        HotkeyConflictMessage = null;
    }

    // ── QR ──────────────────────────────────────────────────────────────────

    public int TargetSizePx
    {
        get => _working.Qr.TargetSizePx;
        set { _working.Qr.TargetSizePx = value; OnPropertyChanged(); }
    }

    public string EccLevel
    {
        // Translate the stored letter ("Q") to/from the display string in the ComboBox
        get => _working.Qr.EccLevel switch
        {
            "L" => "L — 7% recovery",
            "M" => "M — 15% recovery",
            "H" => "H — 30% recovery",
            _   => "Q — 25% recovery (default)",
        };
        set { _working.Qr.EccLevel = value.Length > 0 ? value[0].ToString() : "Q"; OnPropertyChanged(); }
    }

    public IEnumerable<string> EccLevelOptions { get; } =
        ["L — 7% recovery", "M — 15% recovery", "Q — 25% recovery (default)", "H — 30% recovery"];

    // ── Overlay ─────────────────────────────────────────────────────────────

    public bool AutoDismissEnabled
    {
        get => _working.Overlay.AutoDismissSeconds > 0;
        set
        {
            _working.Overlay.AutoDismissSeconds = value ? 5 : 0;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AutoDismissSeconds));
        }
    }

    public int AutoDismissSeconds
    {
        get => _working.Overlay.AutoDismissSeconds > 0 ? _working.Overlay.AutoDismissSeconds : 5;
        set { _working.Overlay.AutoDismissSeconds = AutoDismissEnabled ? value : 0; OnPropertyChanged(); }
    }

    // ── Startup ─────────────────────────────────────────────────────────────

    public bool Autostart
    {
        get => _working.Autostart;
        set { _working.Autostart = value; OnPropertyChanged(); }
    }

    public bool ShowOcrButton
    {
        get => _working.Overlay.ShowOcrButton;
        set { _working.Overlay.ShowOcrButton = value; OnPropertyChanged(); }
    }

    // ── Symbol Filter ────────────────────────────────────────────────────────

    public ObservableCollection<SanitizerRuleViewModel> Rules { get; }

    // ── Commands ─────────────────────────────────────────────────────────────

    public ICommand ApplyCommand  { get; }
    public ICommand CancelCommand { get; }
    public ICommand AddRuleCommand { get; }

    public Action? RequestClose { get; set; }

    private void Apply()
    {
        SyncRulesToWorking();
        try
        {
            _hotkeyService.Register(_working.Hotkey.ParseModifiers(), _working.Hotkey.ParseKey());
            HotkeyConflictMessage = null;
        }
        catch (InvalidOperationException)
        {
            HotkeyConflictMessage = "This hotkey is in use by another application.";
            return;
        }

        _settingsService.Save(_working);
        _settingsService.ApplyAutostart(_working.Autostart);
        RequestClose?.Invoke();
    }

    private void Cancel() => RequestClose?.Invoke();

    private void AddRule()
    {
        var vm = new SanitizerRuleViewModel(new SanitizerRule("", ""));
        vm.PropertyChanged += (_, _) => SyncRulesToWorking();
        Rules.Add(vm);
    }

    public void RemoveRule(SanitizerRuleViewModel rule) => Rules.Remove(rule);

    private void SyncRulesToWorking() =>
        _working.Sanitizer.Rules = Rules.Select(r => r.ToRule()).ToList();

    private void RefreshHotkeyDisplay() =>
        HotkeyDisplay = $"{_working.Hotkey.Modifiers.Replace(",", " + ")} + {_working.Hotkey.Key}";

    // Returns the current committed settings (after Apply)
    public AppSettings GetSettings() => _working;

    private static AppSettings DeepCopy(AppSettings src) => new()
    {
        Hotkey    = new HotkeyConfig { Modifiers = src.Hotkey.Modifiers, Key = src.Hotkey.Key },
        Qr        = new QrCodeConfig { TargetSizePx = src.Qr.TargetSizePx, EccLevel = src.Qr.EccLevel },
        Overlay   = new OverlayConfig { AutoDismissSeconds = src.Overlay.AutoDismissSeconds, ShowOcrButton = src.Overlay.ShowOcrButton },
        Autostart = src.Autostart,
        Sanitizer = new SanitizerConfig { Rules = src.Sanitizer.Rules.Select(r => r with { }).ToList() },
    };

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ── Supporting types ────────────────────────────────────────────────────────

internal sealed class SanitizerRuleViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _match;
    private string _replace;
    private bool _isRegex;

    public SanitizerRuleViewModel(SanitizerRule rule)
    {
        _match   = rule.Match;
        _replace = rule.Replace;
        _isRegex = rule.IsRegex;
    }

    public string Match
    {
        get => _match;
        set { _match = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Match))); }
    }

    public string Replace
    {
        get => _replace;
        set { _replace = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Replace))); }
    }

    public bool IsRegex
    {
        get => _isRegex;
        set { _isRegex = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRegex))); }
    }

    public SanitizerRule ToRule() => new(_match, _replace, _isRegex);
}

internal sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    public RelayCommand(Action execute) => _execute = execute;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}
