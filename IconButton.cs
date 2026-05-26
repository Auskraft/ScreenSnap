using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenSnap
{
    public sealed class IconButton : Button
    {
        private readonly bool _isClose;

        public IconButton(string text, int width, int height, bool isClose = false)
        {
            _isClose  = isClose;
            Text      = text;
            Size      = new Size(width, height);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize      = 0;
            FlatAppearance.MouseOverBackColor  = Color.Transparent;
            FlatAppearance.MouseDownBackColor  = Color.Transparent;
            BackColor = Color.Transparent;
            Cursor    = Cursors.Hand;
            TabStop   = false;
        }

        public void ApplyTheme(AppTheme t)
        {
            ForeColor = t.Text2;
            Font      = t.FontBody(FontLoader.BodyXS);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g   = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool hover = ClientRectangle.Contains(PointToClient(Cursor.Position));
            bool down  = MouseButtons == MouseButtons.Left && hover;

            if (hover)
            {
                var bg = _isClose
                    ? Color.FromArgb(down ? 180 : 120, 220, 50, 50)
                    : Color.FromArgb(down ? 40  : 22,  255, 255, 255);

                using var path = DrawingHelpers.RoundedRect(
                    new RectangleF(1, 1, Width - 2, Height - 2), AppTheme.RXs);
                using var br = new SolidBrush(bg);
                g.FillPath(br, path);
            }

            TextRenderer.DrawText(g, Text, Font,
                ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e)   { base.OnMouseUp(e);   Invalidate(); }
    }
}