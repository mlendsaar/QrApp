using System.Windows;
using System.Windows.Input;

namespace QrApp;

public sealed partial class OverlayWindow : Window
{
    private readonly OverlayViewModel _vm;
    private readonly OcrService _ocrService;
    private readonly TextSanitizerService _sanitizerService;

    private bool _suppressDeactivate;

    internal OverlayWindow(OverlayViewModel vm, OcrService ocrService, TextSanitizerService sanitizerService, bool showOcrButton = false)
    {
        _vm               = vm;
        _ocrService       = ocrService;
        _sanitizerService = sanitizerService;

        vm.RequestClose = Hide;  // X button and Escape hide, not close
        DataContext = vm;
        InitializeComponent();

        OcrButton.Visibility = showOcrButton ? Visibility.Visible : Visibility.Collapsed;

        // Keep placeholder in sync with text content
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OverlayViewModel.SourceText))
                PlaceholderText.Visibility = string.IsNullOrEmpty(vm.SourceText)
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
        };

        SourceTextBox.Focus();
    }

    public void SetInitialText(string text)
    {
        _vm.SourceText = text;
        SourceTextBox.CaretIndex = text.Length;
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        // Guard: don't act if already hidden or mid-close (avoids re-entrancy crash)
        if (!_suppressDeactivate && IsVisible)
            Hide();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

    private async void OcrButton_Click(object sender, RoutedEventArgs e)
    {
        _suppressDeactivate = true;
        Hide();

        System.Drawing.Rectangle? rect;
        try
        {
            rect = await RegionSelectorWindow.SelectAsync();
        }
        finally
        {
            _suppressDeactivate = false;
        }

        if (rect.HasValue)
        {
            var raw       = await _ocrService.RecognizeRegionAsync(rect.Value);
            var sanitized = _sanitizerService.Sanitize(raw);
            _vm.SourceText = sanitized;
        }

        Show();
        Activate();
        SourceTextBox.Focus();
    }

    // Centers the window on the monitor that contains the cursor
    public void PositionNearCursor()
    {
        NativeMethods.GetCursorPos(out var pt);

        var monitorHandle = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        NativeMethods.GetMonitorInfo(monitorHandle, ref info);

        double workLeft  = info.rcWork.Left;
        double workTop   = info.rcWork.Top;
        double workWidth = info.rcWork.Right  - info.rcWork.Left;
        double workHeight= info.rcWork.Bottom - info.rcWork.Top;

        Left = workLeft + (workWidth  - Width)  / 2;
        Top  = workTop  + (workHeight - Height) / 2;
    }
}
