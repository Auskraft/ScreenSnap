using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScreenSnap
{
    public class AppSettings
    {
        // ── Пути ─────────────────────────────────────────────────────────────
        [JsonPropertyName("saveFolder")]
        public string SaveFolder { get; set; } =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "AuskraftSnap");

        /// Алиас для обратной совместимости со старым SettingsForm.cs и ScreenCapture.cs
        [JsonIgnore]
        public string SavePath
        {
            get => SaveFolder;
            set => SaveFolder = value;
        }

        [JsonPropertyName("fileFormat")]
        public string FileFormat { get; set; } = "png";

        // ── Тема (Фаза 1) ────────────────────────────────────────────────────
        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "Dark";

        [JsonPropertyName("accent")]
        public string Accent { get; set; } = "Violet";

        // ── Язык ─────────────────────────────────────────────────────────────
        [JsonPropertyName("language")]
        public string Language { get; set; } = "ru";

        // ── Хоткеи ───────────────────────────────────────────────────────────
        [JsonPropertyName("hotkeyFullScreen")]
        public string HotkeyFullScreen { get; set; } = "Ctrl+Shift+F";

        [JsonPropertyName("hotkeyRegion")]
        public string HotkeyRegion { get; set; } = "Ctrl+Shift+A";

        [JsonPropertyName("hotkeyQuickShare")]
        public string HotkeyQuickShare { get; set; } = "Ctrl+Shift+S";

        [JsonPropertyName("hotkeyCommandPalette")]
        public string HotkeyCommandPalette { get; set; } = "Ctrl+K";

        // ── Яндекс.Диск ──────────────────────────────────────────────────────
        [JsonPropertyName("yandexToken")]
        public string YandexToken { get; set; } = "";

        [JsonPropertyName("autoUpload")]
        public bool AutoUpload { get; set; } = false;

        // ── Прочее ───────────────────────────────────────────────────────────
        [JsonPropertyName("autoStart")]
        public bool AutoStart { get; set; } = false;

        [JsonPropertyName("copyToClipboard")]
        public bool CopyToClipboard { get; set; } = true;

        // ── Load / Save ───────────────────────────────────────────────────────
        private static readonly string SettingsPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        public static AppSettings Load()
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            try
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
            }
            catch { return new AppSettings(); }
        }

        public void Save()
        {
            try { File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOpts)); }
            catch { }
        }
    }
}