using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace QrApp;

internal sealed class OcrService
{
    private readonly OcrEngine _engine =
        OcrEngine.TryCreateFromUserProfileLanguages() ??
        OcrEngine.TryCreateFromLanguage(new Language("en"))!;

    public async Task<string> RecognizeCursorRegionAsync()
    {
        NativeMethods.GetCursorPos(out var pt);
        var region = new System.Drawing.Rectangle(pt.X - 300, pt.Y - 200, 600, 400);
        return await RecognizeRegionAsync(region);
    }

    public async Task<string> RecognizeRegionAsync(System.Drawing.Rectangle screenRect)
    {
        using var bmp = new System.Drawing.Bitmap(screenRect.Width, screenRect.Height);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
            g.CopyFromScreen(screenRect.Location, System.Drawing.Point.Empty, screenRect.Size);

        return await RecognizeBitmapAsync(bmp);
    }

    private async Task<string> RecognizeBitmapAsync(System.Drawing.Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Seek(0, SeekOrigin.Begin);

        var decoder = await BitmapDecoder.CreateAsync(ms.AsRandomAccessStream());
        var soft    = await decoder.GetSoftwareBitmapAsync();
        var result  = await _engine.RecognizeAsync(soft);
        return string.Join(" ", result.Lines.Select(l => l.Text));
    }
}
