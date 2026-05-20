using System.Drawing;
using System.Windows.Forms;

namespace ScreenSnap
{
    public static class ScreenCapture
    {
        public static string SavePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), 
            "ScreenSnap");

        public static string CaptureFullScreen()
        {
            var bounds = Screen.PrimaryScreen!.Bounds;
            using var bitmap = new Bitmap(bounds.Width, bounds.Height);
            using var g = Graphics.FromImage(bitmap);
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

            Directory.CreateDirectory(SavePath);
            var fileName = Path.Combine(SavePath, $"screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
            bitmap.Save(fileName);
            return fileName;
        }
    }
}