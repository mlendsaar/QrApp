using System.Windows;
using KeyEventArgs        = System.Windows.Input.KeyEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseEventArgs      = System.Windows.Input.MouseEventArgs;

namespace QrApp;

public sealed partial class RegionSelectorWindow : Window
{
    private readonly TaskCompletionSource<System.Drawing.Rectangle?> _tcs;
    private System.Windows.Point _start;
    private bool _dragging;

    private RegionSelectorWindow(TaskCompletionSource<System.Drawing.Rectangle?> tcs)
    {
        _tcs = tcs;
        InitializeComponent();

        Left   = SystemParameters.VirtualScreenLeft;
        Top    = SystemParameters.VirtualScreenTop;
        Width  = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        // Position instruction text at 80% vertical, centered
        Loaded += (_, _) =>
        {
            Canvas.SetLeft(InstructionText, (Width - InstructionText.ActualWidth) / 2);
            Canvas.SetTop(InstructionText, Height * 0.80);
        };
    }

    public static Task<System.Drawing.Rectangle?> SelectAsync()
    {
        var tcs = new TaskCompletionSource<System.Drawing.Rectangle?>();
        var win = new RegionSelectorWindow(tcs);
        win.Show();
        win.Focus();
        return tcs.Task;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _tcs.TrySetResult(null);
            Close();
            e.Handled = true;
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _start   = e.GetPosition(this);
        _dragging = true;
        CaptureMouse();

        Canvas.SetLeft(SelectionRect, _start.X);
        Canvas.SetTop(SelectionRect, _start.Y);
        SelectionRect.Width      = 0;
        SelectionRect.Height     = 0;
        SelectionRect.Visibility = Visibility.Visible;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_dragging) return;
        UpdateRect(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();

        var end = e.GetPosition(this);
        int x = (int)(Math.Min(_start.X, end.X) + Left);
        int y = (int)(Math.Min(_start.Y, end.Y) + Top);
        int w = (int)Math.Abs(end.X - _start.X);
        int h = (int)Math.Abs(end.Y - _start.Y);

        // Treat a near-zero drag as a misclick (user clicked but didn't drag a
        // region). 4 px threshold avoids returning a 1×1 rect that OCR cannot use.
        var rect = w > 4 && h > 4
            ? (System.Drawing.Rectangle?)new System.Drawing.Rectangle(x, y, w, h)
            : null;

        _tcs.TrySetResult(rect);
        Close();
    }

    private void UpdateRect(System.Windows.Point current)
    {
        double x = Math.Min(_start.X, current.X);
        double y = Math.Min(_start.Y, current.Y);
        double w = Math.Abs(current.X - _start.X);
        double h = Math.Abs(current.Y - _start.Y);

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width  = w;
        SelectionRect.Height = h;
    }
}
