using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace QrApp;

public sealed partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _vm;
    private readonly QrCodeService _qrService;

    internal SettingsWindow(SettingsViewModel vm, QrCodeService qrService)
    {
        _vm       = vm;
        _qrService = qrService;

        vm.RequestClose = Close;
        DataContext     = vm;

        InitializeComponent();

        // Wire slider → live QR preview
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SettingsViewModel.TargetSizePx) or nameof(SettingsViewModel.EccLevel))
                UpdateQrPreview();
        };
        UpdateQrPreview();
    }

    private void UpdateQrPreview()
    {
        try
        {
            var settings = new QrSettings(_vm.TargetSizePx, _vm.EccLevel switch
            {
                string s when s.StartsWith("L") => QRCoder.QRCodeGenerator.ECCLevel.L,
                string s when s.StartsWith("M") => QRCoder.QRCodeGenerator.ECCLevel.M,
                string s when s.StartsWith("H") => QRCoder.QRCodeGenerator.ECCLevel.H,
                _                                => QRCoder.QRCodeGenerator.ECCLevel.Q,
            });
            QrPreviewImage.Source = _qrService.Generate("QrApp", settings);
        }
        catch { QrPreviewImage.Source = null; }
    }

    // Hotkey recording
    private void HotkeyBox_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _vm.StartRecording();
        HotkeyBox.Background = SystemColors.HighlightBrush.Clone();
        HotkeyBox.Background.Opacity = 0.15;
        e.Handled = true;
    }

    private void HotkeyBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (!_vm.IsRecordingHotkey) return;

        if (e.Key == Key.Escape)
        {
            _vm.CancelRecording();
            HotkeyBox.ClearValue(System.Windows.Controls.TextBox.BackgroundProperty);
            e.Handled = true;
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            return; // modifiers only — wait for the actual key

        _vm.RecordHotkey(Keyboard.Modifiers, key);
        HotkeyBox.ClearValue(System.Windows.Controls.TextBox.BackgroundProperty);
        e.Handled = true;
    }

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: SanitizerRuleViewModel rule })
            _vm.RemoveRule(rule);
    }
}
