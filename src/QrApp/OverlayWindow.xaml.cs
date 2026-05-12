using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace QrApp;

public sealed partial class OverlayWindow : Window
{
    private readonly OverlayViewModel _vm;
    private readonly OcrService _ocrService;
    private readonly TextSanitizerService _sanitizerService;
    private readonly SelectionService _selectionService;

    // Set true while a modal child window (Help, region selector) is on top so
    // OnDeactivated does not auto-hide the overlay underneath it.
    private bool _suppressDeactivate;

    // When true, the overlay stays visible even when it loses focus.
    private bool _pinned;

    private readonly OcrConfig _ocrConfig;

    private readonly DispatcherTimer _clipboardWatchTimer;
    private string _lastWatchedClipboardText = string.Empty;

    internal OverlayWindow(
        OverlayViewModel vm,
        OcrService ocrService,
        TextSanitizerService sanitizerService,
        SelectionService selectionService,
        bool showOcrButton = false,
        OcrConfig? ocrConfig = null,
        int targetSizePx = 300,
        bool pinOverlay = false,
        bool watchClipboard = false)
    {
        _vm                = vm;
        _ocrService        = ocrService;
        _sanitizerService  = sanitizerService;
        _selectionService  = selectionService;
        _ocrConfig         = ocrConfig ?? new OcrConfig();

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

        _pinned             = pinOverlay;
        PinToggle.IsChecked = pinOverlay;

        _clipboardWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _clipboardWatchTimer.Tick += ClipboardWatchTimer_Tick;
        WatchToggle.IsChecked = watchClipboard;

        // Keep placeholder in sync with text content
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OverlayViewModel.SourceText))
                PlaceholderText.Visibility = string.IsNullOrEmpty(vm.SourceText)
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
        };

        Closed += (_, _) => _clipboardWatchTimer.Stop();
        SourceTextBox.Focus();
    }

    public void SetInitialText(string text)
    {
        _vm.SourceText = text;
        SourceTextBox.CaretIndex = text.Length;
        // Seed the clipboard-watch baseline so re-pressing the hotkey while
        // watching doesn't immediately trigger a redundant "change detected".
        _lastWatchedClipboardText = text;
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        // Guard: don't act if already hidden or mid-close (avoids re-entrancy crash)
        if (_pinned || _suppressDeactivate || !IsVisible) return;
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

    private void PinToggle_Changed(object sender, RoutedEventArgs e)
    {
        _pinned = PinToggle.IsChecked == true;
    }

    private void WatchToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (WatchToggle.IsChecked == true)
        {
            // Capture the current text as the baseline so we only react to
            // clipboard changes that happen after the toggle is enabled.
            _lastWatchedClipboardText = _vm.SourceText;
            _clipboardWatchTimer.Start();
        }
        else
        {
            _clipboardWatchTimer.Stop();
        }
    }

    private void ClipboardWatchTimer_Tick(object? sender, EventArgs e)
    {
        string raw;
        try { raw = _selectionService.GetClipboardText(); }
        catch { return; }

        var sanitized = _sanitizerService.Sanitize(raw);
        if (string.IsNullOrEmpty(sanitized) || sanitized == _lastWatchedClipboardText) return;

        _lastWatchedClipboardText = sanitized;
        _vm.SourceText            = sanitized;
        SourceTextBox.CaretIndex  = sanitized.Length;
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
