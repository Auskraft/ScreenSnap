using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;

namespace ScreenSnap
{
    /// <summary>
    /// Захват экрана. Фаза 3:
    ///   • CaptureFullScreen(saveFolder) — принимает папку из AppSettings
    ///   • ConvertToWebpAsync(pngPath)   — WEBP через SixLabors.ImageSharp 3.x
    ///   • SetClipboardImage(path)       — копирование в буфер обмена
    /// </summary>
    public static class ScreenCapture
    {
        // Fallback путь (если saveFolder не передан)
        public static string SavePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "AuskraftSnap");

        // ── Capture ───────────────────────────────────────────────────────────

        /// <summary>Полный экран. saveFolder — из AppSettings.SaveFolder.</summary>
        public static string CaptureFullScreen(string? saveFolder = null)
        {
            var folder = saveFolder ?? SavePath;
            var bounds = Screen.PrimaryScreen!.Bounds;

            using var bitmap = new Bitmap(bounds.Width, bounds.Height,
                PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);
            g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);

            Directory.CreateDirectory(folder);
            var path = MakePath(folder, "png");
            bitmap.Save(path, ImageFormat.Png);
            return path;
        }

        /// <summary>Регион экрана. Оставлен для прямых вызовов с Rectangle.</summary>
        public static string CaptureRegion(System.Drawing.Rectangle region, string? saveFolder = null)
        {
            var folder = saveFolder ?? SavePath;

            using var bitmap = new Bitmap(region.Width, region.Height,
                PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);
            g.CopyFromScreen(region.Location, System.Drawing.Point.Empty, region.Size);

            Directory.CreateDirectory(folder);
            var path = MakePath(folder, "png");
            bitmap.Save(path, ImageFormat.Png);
            return path;
        }

        // ── WEBP ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Конвертирует PNG в WEBP через SixLabors.ImageSharp.
        /// Качество: 90 по умолчанию (меняется в AppSettings в Фазе 4).
        /// Возвращает путь к .webp, либо null при ошибке.
        /// </summary>
        public static async Task<string?> ConvertToWebpAsync(string sourcePath, int quality = 90)
        {
            if (!File.Exists(sourcePath)) return null;

            var destPath = Path.ChangeExtension(sourcePath, ".webp");
            try
            {
                using var image = await SixLabors.ImageSharp.Image.LoadAsync(sourcePath);
                var encoder = new WebpEncoder { Quality = quality };
                await image.SaveAsWebpAsync(destPath, encoder);
                return destPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ScreenCapture] WEBP failed: {ex.Message}");
                return null;
            }
        }

        // ── Clipboard ─────────────────────────────────────────────────────────

        /// <summary>Копирует изображение в буфер обмена. Вызывать из UI-потока.</summary>
        public static void SetClipboardImage(string path)
        {
            if (!File.Exists(path)) return;
            try
            {
                using var bmp = new Bitmap(path);
                Clipboard.SetImage(bmp);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ScreenCapture] Clipboard error: {ex.Message}");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string MakePath(string folder, string ext)
            => Path.Combine(folder,
                $"screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.{ext}");
    }
}