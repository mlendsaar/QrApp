using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace QrApp;

internal sealed class OverlayViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly QrCodeService _qrService;
    private QrSettings _qrSettings;
    private readonly DispatcherTimer _debounce;

    private BitmapSource? _qrImage;
    private string _sourceText = string.Empty;
    private string _statusText = string.Empty;
    private StatusLevel _statusLevel = StatusLevel.None;

    public OverlayViewModel(QrCodeService qrService, QrSettings qrSettings)
    {
        _qrService  = qrService;
        _qrSettings = qrSettings;

        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); RegenerateQr(); };
    }

    public BitmapSource? QrImage
    {
        get => _qrImage;
        private set { _qrImage = value; OnPropertyChanged(); }
    }

    public string SourceText
    {
        get => _sourceText;
        set
        {
            if (_sourceText == value) return;
            _sourceText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ByteCountText));
            _debounce.Stop();
            _debounce.Start();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    public StatusLevel StatusLevel
    {
        get => _statusLevel;
        private set { _statusLevel = value; OnPropertyChanged(); }
    }

    public string ByteCountText
    {
        get
        {
            int bytes   = Encoding.UTF8.GetByteCount(_sourceText);
            int maxBytes = QrCodeService.GetMaxBytes(_qrSettings.EccLevel);
            return $"{bytes} / {maxBytes} bytes";
        }
    }

    public void UpdateSettings(QrSettings settings)
    {
        _qrSettings = settings;
        OnPropertyChanged(nameof(ByteCountText));
        RegenerateQr();
    }

    private void RegenerateQr()
    {
        if (string.IsNullOrWhiteSpace(_sourceText))
        {
            QrImage     = null;
            StatusText  = string.Empty;
            StatusLevel = StatusLevel.None;
            return;
        }

        int bytes    = Encoding.UTF8.GetByteCount(_sourceText);
        int maxBytes = QrCodeService.GetMaxBytes(_qrSettings.EccLevel);
        double pct   = (double)bytes / maxBytes;

        try
        {
            QrImage = _qrService.Generate(_sourceText, _qrSettings);

            if (pct >= 1.0)
            {
                StatusText  = "Too much data — edit the text to reduce it.";
                StatusLevel = StatusLevel.Error;
            }
            else if (pct >= 0.8)
            {
                StatusText  = "Approaching QR capacity — consider reducing text or switching to ECC L.";
                StatusLevel = StatusLevel.Warning;
            }
            else
            {
                StatusText  = string.Empty;
                StatusLevel = StatusLevel.None;
            }
        }
        catch
        {
            StatusText  = "Too much data — edit the text to reduce it.";
            StatusLevel = StatusLevel.Error;
            QrImage     = null;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal enum StatusLevel { None, Warning, Error }
