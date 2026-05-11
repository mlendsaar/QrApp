using System.Windows;
using System.Windows.Input;

namespace QrApp;

public sealed partial class OverlayWindow : Window
{
    private readonly OverlayViewModel _vm;
    private readonly OcrService _ocrService;
    private readonly TextSanitizerService _sanitizerService;

    private bool _suppressDeactivate;

    internal OverlayWindow(OverlayViewModel vm, OcrService ocrService, TextSanitizerService sanitizerService)
    {
        _vm               = vm;
        _ocrService       = ocrService;
        _sanitizerService = sanitizerService;

        vm.RequestClose = Close;
        DataContext = vm;
        InitializeComponent();

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
        if (!_suppressDeactivate)
            Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

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

    // Positions the window 16 px to the right of the cursor, clamped to monitor work area
    public void PositionNearCursor()
    {
        NativeMethods.GetCursorPos(out var pt);

        var monitorHandle = NativeMethods.MonitorFromPoint(pt, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        NativeMethods.GetMonitorInfo(monitorHandle, ref info);

        double workLeft   = info.rcWork.Left;
        double workTop    = info.rcWork.Top;
        double workRight  = info.rcWork.Right;
        double workBottom = info.rcWork.Bottom;

        double desiredLeft = pt.X + 16;
        double desiredTop  = pt.Y;

        // Flip left if not enough room on the right
        if (desiredLeft + Width > workRight)
            desiredLeft = pt.X - Width - 16;

        Left = Math.Max(workLeft,  Math.Min(desiredLeft, workRight  - Width));
        Top  = Math.Max(workTop,   Math.Min(desiredTop,  workBottom - Height));
    }
}
