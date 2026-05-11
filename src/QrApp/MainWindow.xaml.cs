using System.Windows;

namespace QrApp;

/// <summary>
/// Hidden host window used as the message pump anchor for the system tray and hotkey service.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
