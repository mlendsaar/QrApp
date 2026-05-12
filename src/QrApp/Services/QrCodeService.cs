using QRCoder;
using System.IO;
using System.Windows.Media.Imaging;

namespace QrApp;

internal sealed class QrCodeService
{
    public BitmapSource Generate(string text, QrSettings settings)
    {
        using var generator = new QRCodeGenerator();
        using var data      = generator.CreateQrCode(text, settings.EccLevel);

        // QRCoder expects pixels-per-module, but the user configures a target image size.
        // The module-matrix dimension depends on text length and ECC level, so we derive
        // PPM after generation. Ceiling + Max(1, …) keeps the QR readable for short input.
        int moduleCount     = data.ModuleMatrix.Count;
        int ppm             = Math.Max(1, (int)Math.Ceiling((double)settings.TargetSizePx / moduleCount));
        using var code      = new PngByteQRCode(data);
        byte[] png          = code.GetGraphic(ppm);

        var image = new BitmapImage();
        using var ms = new MemoryStream(png);
        image.BeginInit();
        image.CacheOption  = BitmapCacheOption.OnLoad;
        image.StreamSource = ms;
        image.EndInit();
        image.Freeze();
        return image;
    }

    // QR v40 byte-mode capacity per ECC level (ISO/IEC 18004). Used by the
    // status-bar threshold checks to warn before generation would overflow.
    public static int GetMaxBytes(QRCodeGenerator.ECCLevel eccLevel) => eccLevel switch
    {
        QRCodeGenerator.ECCLevel.L => 2953,
        QRCodeGenerator.ECCLevel.M => 2331,
        QRCodeGenerator.ECCLevel.Q => 1663,
        QRCodeGenerator.ECCLevel.H => 1273,
        _                          => 1663,
    };
}

record QrSettings(int TargetSizePx = 300, QRCodeGenerator.ECCLevel EccLevel = QRCodeGenerator.ECCLevel.Q);
