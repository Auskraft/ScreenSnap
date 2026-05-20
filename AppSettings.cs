using System.Text.Json;

namespace ScreenSnap
{
    public class AppSettings
    {
        public string SavePath { get; set; } = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AuskraftSnap");
        public string FileFormat { get; set; } = "PNG";
        public string HotkeyFullScreen { get; set; } = "Ctrl+Shift+F";
        public string HotkeyRegion { get; set; } = "Ctrl+Shift+A";
        public string YandexToken { get; set; } = "";

        private static readonly string SettingsPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            return new AppSettings();
        }

        public void Save()
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
    }
}