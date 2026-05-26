using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;

namespace ScreenSnap
{
    /// <summary>
    /// Загружает Space Grotesk, Inter, JetBrains Mono из папки fonts/ рядом с exe.
    /// Fallback на системные шрифты если файлы не найдены.
    ///
    /// Вызови FontLoader.Initialize() один раз в Program.cs до Application.Run().
    /// </summary>
    public static class FontLoader
    {
        private static readonly PrivateFontCollection _pfc = new();
        private static bool _initialized;

        private static string? _displayFamily;
        private static string? _bodyFamily;
        private static string? _monoFamily;

        private const string FallbackDisplay = "Segoe UI";
        private const string FallbackBody    = "Segoe UI";
        private const string FallbackMono    = "Consolas";

        // ── Константы размеров (из TOKENS.md §5) ────────────────────────────
        public const float DisplayXL = 28f;
        public const float DisplayL  = 26f;
        public const float DisplayM  = 22f;
        public const float DisplayS  = 18f;
        public const float DisplayXS = 16f;
        public const float BodyL     = 13.5f;
        public const float BodyM     = 13f;
        public const float BodyS     = 12.5f;
        public const float BodyXS    = 12f;
        public const float Caption   = 11.5f;
        public const float CaptionXS = 11f;
        public const float MonoLabel = 10.5f;
        public const float MonoSmall = 10f;

        // ── Initialize ───────────────────────────────────────────────────────
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            var fontsDir = Path.Combine(
                Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!,
                "fonts");

            if (Directory.Exists(fontsDir))
            {
                foreach (var file in Directory.GetFiles(fontsDir, "*.ttf"))
                {
                    try { _pfc.AddFontFile(file); }
                    catch { /* пропустить повреждённый файл */ }
                }
            }

            var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (FontFamily ff in _pfc.Families)
                loaded.Add(ff.Name);

            _displayFamily = loaded.Contains("Space Grotesk") ? "Space Grotesk" : null;
            _bodyFamily    = loaded.Contains("Inter")          ? "Inter"         : null;
            _monoFamily    = loaded.Contains("JetBrains Mono") ? "JetBrains Mono": null;
        }

        // ── Публичный API ────────────────────────────────────────────────────

        /// Space Grotesk — заголовки, бренд, большие числа
        public static Font GetDisplay(float size, FontStyle style = FontStyle.Regular)
            => GetFont(_displayFamily, FallbackDisplay, size, style);

        /// Inter — основной текст, кнопки
        public static Font GetBody(float size, FontStyle style = FontStyle.Regular)
            => GetFont(_bodyFamily, FallbackBody, size, style);

        /// JetBrains Mono — хоткеи, имена файлов, временны́е метки
        public static Font GetMono(float size, FontStyle style = FontStyle.Regular)
            => GetFont(_monoFamily, FallbackMono, size, style);

        // ── Внутренний поиск ─────────────────────────────────────────────────
        private static Font GetFont(string? preferred, string fallback,
            float size, FontStyle style)
        {
            if (!_initialized)
                throw new InvalidOperationException("FontLoader.Initialize() не вызван.");

            // Ищем в приватной коллекции
            if (preferred != null)
            {
                foreach (FontFamily ff in _pfc.Families)
                {
                    if (!ff.Name.Equals(preferred, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Если нужный стиль недоступен — берём Regular
                    var actualStyle = ff.IsStyleAvailable(style) ? style : FontStyle.Regular;
                    if (ff.IsStyleAvailable(actualStyle))
                        return new Font(ff, size, actualStyle, GraphicsUnit.Point);
                }
            }

            // Fallback — системный шрифт
            return new Font(fallback, size, style, GraphicsUnit.Point);
        }
    }
}