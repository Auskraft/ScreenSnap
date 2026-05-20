using System.Drawing;
using System.Windows.Forms;

namespace ScreenSnap
{
    public class RegionSelector : Form
    {
        private Point startPoint;
        private Rectangle selectedRegion;
        private bool isSelecting = false;

        private Rectangle GetSelectedRegion() => selectedRegion;

        public static string? CaptureRegion()
        {
            using var selector = new RegionSelector();

            if (selector.ShowDialog() == DialogResult.OK)
            {
                var region = selector.GetSelectedRegion();
                if (region.Width > 0)
                    return ScreenCapture.CaptureRegion(region);
            }

            return null;
        }

        private RegionSelector()
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
            Cursor = Cursors.Cross;
            DoubleBuffered = true;
            BackColor = Color.Black;
            Opacity = 0.4;

            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel; };
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            Paint += OnPaint;
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            startPoint = e.Location;
            isSelecting = true;
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (!isSelecting) return;
            selectedRegion = GetRect(startPoint, e.Location);
            Invalidate();
        }

        private void OnMouseUp(object? sender, MouseEventArgs e)
        {
            selectedRegion = GetRect(startPoint, e.Location);
            DialogResult = DialogResult.OK;
        }

        private void OnPaint(object? sender, PaintEventArgs e)
        {
            if (selectedRegion.Width > 0 && selectedRegion.Height > 0)
            {
                e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(80, Color.White)), selectedRegion);
                e.Graphics.DrawRectangle(new Pen(Color.Red, 2), selectedRegion);
            }
        }

        private static Rectangle GetRect(Point a, Point b) =>
            new Rectangle(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
                          Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    }
}