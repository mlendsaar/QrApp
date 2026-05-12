using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace QrApp;

internal sealed class OcrService
{
    // Prefer the user's installed language packs. Fall back to English so the
    // service still works on minimal Windows installs that lack OCR languages.
    private readonly OcrEngine _engine =
        OcrEngine.TryCreateFromUserProfileLanguages() ??
        OcrEngine.TryCreateFromLanguage(new Language("en"))!;

    public async Task<string> RecognizeRegionAsync(System.Drawing.Rectangle screenRect, OcrConfig? config = null)
    {
        using var bmp = new System.Drawing.Bitmap(screenRect.Width, screenRect.Height);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
            g.CopyFromScreen(screenRect.Location, System.Drawing.Point.Empty, screenRect.Size);

        var toRecognize = (config?.UpscaleEnabled ?? true) ? Upscale(bmp) : bmp;
        try
        {
            return await RecognizeBitmapAsync(toRecognize, config?.PreserveLines ?? true);
        }
        finally
        {
            if (!ReferenceEquals(toRecognize, bmp))
                toRecognize.Dispose();
        }
    }

    private static System.Drawing.Bitmap Upscale(System.Drawing.Bitmap bmp)
    {
        int maxDim = Math.Max(bmp.Width, bmp.Height);
        if (maxDim == 0) return bmp;

        // Windows OCR max dimension is 5000 px; clamp scale so we stay under 4800
        int scale = Math.Min(3, 4800 / maxDim);
        if (scale <= 1) return bmp;

        var scaled = new System.Drawing.Bitmap(bmp.Width * scale, bmp.Height * scale);
        using var g = System.Drawing.Graphics.FromImage(scaled);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(bmp, 0, 0, scaled.Width, scaled.Height);
        return scaled;
    }

    private async Task<string> RecognizeBitmapAsync(System.Drawing.Bitmap bmp, bool preserveLines)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Seek(0, SeekOrigin.Begin);

        var decoder = await BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
        var soft    = await decoder.GetSoftwareBitmapAsync();
        var result  = await _engine.RecognizeAsync(soft);
        var sep     = preserveLines ? "\n" : " ";
        return string.Join(sep, result.Lines.Select(l => l.Text));
    }
}
