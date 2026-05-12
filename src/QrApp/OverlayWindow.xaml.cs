using System.Windows;
using System.Windows.Input;

namespace QrApp;

public sealed partial class OverlayWindow : Window
{
    private readonly OverlayViewModel _vm;
    private readonly OcrService _ocrService;
    private readonly TextSanitizerService _sanitizerService;

    // Set true while a modal child window (Help, region selector) is on top so
    // OnDeactivated does not auto-hide the overlay underneath it.
    private bool _suppressDeactivate;

    private readonly OcrConfig _ocrConfig;

    internal OverlayWindow(OverlayViewModel vm, OcrService ocrService, TextSanitizerService sanitizerService, bool showOcrButton = false, OcrConfig? ocrConfig = null, int targetSizePx = 300)
    {
        _vm               = vm;
        _ocrService       = ocrService;
        _sanitizerService = sanitizerService;
        _ocrConfig        = ocrConfig ?? new OcrConfig();

        vm.RequestClose = Hide;  // X button and Escape hide, not close
        DataContext = vm;
        InitializeComponent();

        // Scale the window so the QR image renders at its requested pixel size.
        // Both columns are *-sized, so the QR column ≈ Width/2. We want that
        // column ≥ targetSizePx + padding (16 px margin around the image).
        // Mirror the same width on the TextBox column for visual balance.
        const int Padding = 16;
        const int Header  = 32;
        const int Status  = 32;
        Width  = 2 * (targetSizePx + Padding) + Padding;
        Height = Header + Padding + targetSizePx + Padding + Status;

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

    private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Walk up from the click source; don't drag if a button was clicked
        var src = e.OriginalSource as System.Windows.DependencyObject;
        while (src is not null && !ReferenceEquals(src, sender))
        {
            if (src is System.Windows.Controls.Primitives.ButtonBase) return;
            src = System.Windows.Media.VisualTreeHelper.GetParent(src);
        }
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        _suppressDeactivate = true;
        var help = new HelpWindow { Owner = this };
        help.Closed += (_, _) => _suppressDeactivate = false;
        help.Show();
    }

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
            var raw       = await _ocrService.RecognizeRegionAsync(rect.Value, _ocrConfig);
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
