using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenSnap
{
    public sealed class IconButton : Button
    {
        private readonly bool _isClose;
        private bool _isTextLabel = false;

        public IconButton(string text, int width, int height, bool isClose = false)
        {
            _isClose  = isClose;
            Text      = text;
            Size      = new Size(width, height);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize         = 0;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            BackColor = Color.Transparent;
            Cursor    = Cursors.Hand;
            TabStop   = false;

            _isTextLabel = IsPlainText(text);
            ApplyFont();
        }

        public void ApplyTheme(AppTheme t)
        {
            ForeColor = t.Text2;
            Font = _isTextLabel
                ? t.FontBody(FontLoader.BodyXS, System.Drawing.FontStyle.Bold)
                : new System.Drawing.Font("Segoe UI Emoji", 11f);
            Invalidate();
        }

        public void SetLabel(string label)
        {
            Text         = label;
            _isTextLabel = IsPlainText(label);
            ApplyFont();
            Invalidate();
        }

        private void ApplyFont()
        {
            Font = _isTextLabel
                ? new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold)
                : new System.Drawing.Font("Segoe UI Emoji", 11f);
        }

        private static bool IsPlainText(string s)
        {
            if (string.IsNullOrEmpty(s)) return true;
            foreach (char c in s)
            {
                if (char.IsWhiteSpace(c)) continue;
                if (char.IsLetter(c) || char.IsDigit(c)) continue;
                return false;
            }
            return true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode      = SmoothingMode.AntiAlias;
            g.TextRenderingHint  = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

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

            // ── FIX: Graphics.DrawString корректно рендерит emoji через Segoe UI Emoji
            using var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            using var brush = new SolidBrush(ForeColor);
            g.DrawString(Text, Font, brush, new RectangleF(0, 0, Width, Height), sf);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e)   { base.OnMouseUp(e);   Invalidate(); }
    }
}