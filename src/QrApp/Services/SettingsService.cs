using System.IO;
using System.Text.Json;

namespace QrApp;

internal sealed class SettingsService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QrApp", "settings.json");

    public AppSettings Load()
    {
        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? AppSettings.Default;
        }
        catch   // missing file, locked file, or malformed JSON
        {
            Save(AppSettings.Default);
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath,
            JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void ApplyAutostart(bool enable)
    {
        const string regKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(regKey, writable: true);
        if (enable) key?.SetValue("QrApp", Environment.ProcessPath!);
        else        key?.DeleteValue("QrApp", throwOnMissingValue: false);
    }
}
