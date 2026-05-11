using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace QrApp;

internal sealed class StatusLevelToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is StatusLevel lvl && lvl != StatusLevel.None ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

internal sealed class StatusLevelToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(0xFF, 0xF4, 0xCE));
    private static readonly SolidColorBrush ErrorBrush   = new(Color.FromRgb(0xFD, 0xE7, 0xE9));
    private static readonly SolidColorBrush NoneBrush    = Brushes.Transparent;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is StatusLevel lvl ? lvl switch
        {
            StatusLevel.Warning => WarningBrush,
            StatusLevel.Error   => ErrorBrush,
            _                   => NoneBrush,
        } : NoneBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

internal sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}
