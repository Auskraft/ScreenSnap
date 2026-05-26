using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenSnap
{
    // ═══════════════════════════════════════════════════════════════════════
    //  ShareOverlay — модал после завершения Quick Share / Privacy Mode
    //
    //  Содержит:
    //    • Анимированная галочка (spring-pop 0.55s)
    //    • Имя файла + размер + время выполнения
    //    • URL + кнопка Copy
    //    • Сетка 2×2: Open / Copy as Markdown / Copy as HTML / Show QR
    //    • QR-код 104×104px (через api.qrserver.com)
    //    • Футер: Delete · Dismiss
    // ═══════════════════════════════════════════════════════════════════════
    public sealed class ShareOverlay : Form
    {
        // ── Win32 ────────────────────────────────────────────────────────────
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION       = 0x2;

        // ── Layout ───────────────────────────────────────────────────────────
        private const int W       = 400;
        private const int PadX    = 24;
        private const int Radius  = 16;

        // ── Data ─────────────────────────────────────────────────────────────
        private readonly string _fileName;
        private readonly long   _fileSize;
        private readonly long   _elapsedMs;
        private readonly string _publicUrl;

        // ── UI state ─────────────────────────────────────────────────────────
        private float   _checkScale  = 0f;
        private float   _checkVel    = 0f;
        private bool    _showQr      = false;
        private Bitmap? _qrBitmap    = null;
        private int?    _hoveredBtn  = null; // 0-3 для сетки кнопок, 10=copy url, 11=delete, 12=dismiss

        private readonly System.Windows.Forms.Timer _springTimer;
        private readonly Panel _urlBar;
        private readonly Label _urlLabel;

        // ─────────────────────────────────────────────────────────────────────
        public ShareOverlay(string fileName, long fileSize, long elapsedMs, string publicUrl)
        {
            _fileName  = fileName;
            _fileSize  = fileSize;
            _elapsedMs = elapsedMs;
            _publicUrl = publicUrl;

            // Form
            FormBorderStyle = FormBorderStyle.None;
            Size            = new Size(W, ComputeHeight());
            StartPosition   = FormStartPosition.CenterParent;
            BackColor       = Color.Black;
            DoubleBuffered  = true;
            TopMost         = true;
            ShowInTaskbar   = false;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);

            ApplyRoundedRegion();

            // URL bar
            _urlBar = new Panel
            {
                Location  = new Point(PadX, 188),
                Size      = new Size(W - PadX * 2, 32),
                BackColor = Color.Transparent,
                Cursor    = Cursors.IBeam,
            };
            _urlBar.Paint += OnUrlBarPaint;
            _urlBar.Click += (_, _) => CopyUrl();
            Controls.Add(_urlBar);

            _urlLabel = new Label
            {
                Text      = _publicUrl,
                AutoSize  = false,
                Location  = new Point(PadX + 8, 0),
                Size      = new Size(W - PadX * 2 - 60, 32),
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor    = Cursors.IBeam,
                Font      = new Font("Consolas", 9.5f),
            };
            _urlLabel.Click += (_, _) => CopyUrl();
            _urlBar.Controls.Add(_urlLabel);

            // Spring анимация галочки
            _springTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _springTimer.Tick += OnSpringTick;

            // Подписки
            MouseClick  += OnMouseClick;
            MouseMove   += OnMouseMove;
            MouseLeave  += (_, _) => { _hoveredBtn = null; Invalidate(); };
            MouseDown   += OnMouseDown;

            ThemeManager.ThemeChanged += (_, _) => { ApplyRoundedRegion(); Invalidate(); };
        }

        // ── Height ───────────────────────────────────────────────────────────
        private int ComputeHeight() => _showQr ? 530 : 430;

        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var t = ThemeManager.Current;

            // Фон
            DrawingHelpers.DrawGlassCard(g,
                new RectangleF(0, 0, W, Height), Radius,
                t.BgGlassStrong, t.Stroke2);

            // Тонкая акцентная полоска сверху
            using var stripBrush = new SolidBrush(
                Color.FromArgb(120, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B));
            using var stripPath = DrawingHelpers.RoundedRect(
                new RectangleF(0, 0, W, 2), 0);
            g.FillPath(stripBrush, stripPath);

            int y = 28;

            // ── Галочка ────────────────────────────────────────────────────
            DrawAnimatedCheck(g, t, W / 2, y + 22);
            y += 66;

            // ── Заголовок ──────────────────────────────────────────────────
            using var titleFont = t.FontDisplay(FontLoader.DisplayS, System.Drawing.FontStyle.Bold);
            TextRenderer.DrawText(g, "Успешно загружено", titleFont,
                new Rectangle(0, y, W, 28), t.Text1, TextFormatFlags.HorizontalCenter);
            y += 34;

            // ── Метаданные ─────────────────────────────────────────────────
            using var metaFont = t.FontBody(FontLoader.BodyXS);
            var sizeStr  = FormatSize(_fileSize);
            var timeStr  = $"{_elapsedMs}ms";
            var metaText = $"{_fileName}   ·   {sizeStr}   ·   {timeStr}";
            TextRenderer.DrawText(g, metaText, metaFont,
                new Rectangle(PadX, y, W - PadX * 2, 20), t.Text3,
                TextFormatFlags.HorizontalCenter);
            y += 28;

            // URL bar — панель рендерится отдельно, просто двигаем y
            _urlBar.Location = new Point(PadX, y);
            y += 32 + 16;

            // ── Сетка 2×2 ─────────────────────────────────────────────────
            DrawActionGrid(g, t, PadX, y);
            y += 80 + 16;

            // ── QR ─────────────────────────────────────────────────────────
            if (_showQr)
            {
                DrawQrSection(g, t, (W - 104) / 2, y);
                y += 104 + 16;
            }

            // ── Футер ──────────────────────────────────────────────────────
            DrawFooter(g, t, y);
        }

        // ── Анимированная галочка ─────────────────────────────────────────────
        private void DrawAnimatedCheck(Graphics g, AppTheme t, int cx, int cy)
        {
            float s = _checkScale;
            if (s <= 0) return;

            int r = (int)(22 * s);
            var acc = t.Accent.A2;

            // Круг
            using var circleBrush = new SolidBrush(
                Color.FromArgb((int)(200 * Math.Min(s, 1f)), acc.R, acc.G, acc.B));
            g.FillEllipse(circleBrush, cx - r, cy - r, r * 2, r * 2);

            // Галочка (масштабируем)
            float ps = s > 1 ? 2 - s : s; // обратный bounce
            ps = Math.Max(0, Math.Min(1, ps));
            if (ps < 0.3f) return;

            float lw = 2.5f * ps;
            using var pen = new Pen(Color.White, lw) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            float sc = r * 0.55f;
            g.DrawLines(pen, new[]
            {
                new PointF(cx - sc * 0.5f, cy),
                new PointF(cx - sc * 0.05f, cy + sc * 0.5f),
                new PointF(cx + sc * 0.5f, cy - sc * 0.4f),
            });
        }

        // ── URL bar paint ──────────────────────────────────────────────────────
        private void OnUrlBarPaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var t = ThemeManager.Current;
            DrawingHelpers.DrawGlassCard(g,
                new RectangleF(0, 0, _urlBar.Width, 32), AppTheme.RInput,
                Color.FromArgb(8, 255, 255, 255), t.Stroke1);
        }

        // ── Action grid 2×2 ───────────────────────────────────────────────────
        private static readonly (string Icon, string Label, int Id)[] GridButtons =
        {
            ("🔗", "Open in browser", 0),
            ("MD", "Copy as Markdown", 1),
            ("< >","Copy as HTML",    2),
            ("□", "Show QR code",     3),
        };

        private void DrawActionGrid(Graphics g, AppTheme t, int x, int y)
        {
            int btnW = (W - PadX * 2 - 8) / 2;
            int btnH = 36;

            for (int i = 0; i < GridButtons.Length; i++)
            {
                int col  = i % 2;
                int row  = i / 2;
                int bx   = x + col * (btnW + 8);
                int by   = y + row * (btnH + 8);

                bool hovered = _hoveredBtn == GridButtons[i].Id;
                DrawActionBtn(g, t, GridButtons[i], bx, by, btnW, btnH, hovered);
            }
        }

        private void DrawActionBtn(Graphics g, AppTheme t,
            (string Icon, string Label, int Id) btn,
            int x, int y, int w, int h, bool hovered)
        {
            Color fill = hovered
                ? Color.FromArgb(18, 255, 255, 255)
                : Color.FromArgb(6, 255, 255, 255);
            DrawingHelpers.DrawGlassCard(g, new RectangleF(x, y, w, h),
                AppTheme.RSm, fill, t.Stroke1);

            using var iconFont = t.FontMono(11f);
            using var labelFont = t.FontBody(FontLoader.BodyXS);

            TextRenderer.DrawText(g, btn.Icon, iconFont,
                new Rectangle(x + 10, y, 22, h), hovered ? t.Accent.A2 : t.Text3,
                TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, btn.Label, labelFont,
                new Rectangle(x + 34, y, w - 38, h), hovered ? t.Text1 : t.Text2,
                TextFormatFlags.VerticalCenter);
        }

        // ── QR ────────────────────────────────────────────────────────────────
        private void DrawQrSection(Graphics g, AppTheme t, int x, int y)
        {
            if (_qrBitmap != null)
            {
                DrawingHelpers.DrawGlassCard(g, new RectangleF(x - 8, y - 8, 120, 120),
                    AppTheme.RSm, Color.FromArgb(12, 255, 255, 255), t.Stroke1);
                g.DrawImage(_qrBitmap, x, y, 104, 104);
            }
            else
            {
                DrawingHelpers.DrawGlassCard(g, new RectangleF(x - 8, y - 8, 120, 120),
                    AppTheme.RSm, Color.FromArgb(8, 255, 255, 255), t.Stroke1);
                using var f = t.FontBody(FontLoader.BodyXS);
                TextRenderer.DrawText(g, "Загрузка QR…", f,
                    new Rectangle(x, y, 104, 104), t.Text4,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                LoadQrAsync();
            }
        }

        // ── Футер ──────────────────────────────────────────────────────────────
        private void DrawFooter(Graphics g, AppTheme t, int y)
        {
            int footerY = Height - 48;
            using var divPen = new Pen(t.Stroke1, 0.5f);
            g.DrawLine(divPen, PadX, footerY, W - PadX, footerY);

            using var footFont = t.FontBody(FontLoader.BodyXS);
            var btns = new[] { ("Удалить", 11, PadX), ("Закрыть", 12, W - PadX - 60) };
            foreach (var (label, id, bx) in btns)
            {
                bool hovered = _hoveredBtn == id;
                Color c = id == 11
                    ? (hovered ? Color.FromArgb(220, 80, 60) : t.Text3)
                    : (hovered ? t.Text1 : t.Text3);
                TextRenderer.DrawText(g, label, footFont,
                    new Rectangle(bx, footerY + 12, 60, 24), c);
            }
        }

        // ── Copy URL ───────────────────────────────────────────────────────────
        private void CopyUrl()
        {
            Clipboard.SetText(_publicUrl);
            _urlLabel.Text = "Скопировано ✓";
            _urlLabel.ForeColor = ThemeManager.Current.Accent.A2;
            Task.Delay(1800).ContinueWith(_ => Invoke(() =>
            {
                _urlLabel.Text      = _publicUrl;
                _urlLabel.ForeColor = ThemeManager.Current.Text2;
            }));
        }

        // ── QR async load ─────────────────────────────────────────────────────
        private bool _qrLoading = false;
        private async void LoadQrAsync()
        {
            if (_qrLoading) return;
            _qrLoading = true;
            try
            {
                using var client  = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var encoded       = Uri.EscapeDataString(_publicUrl);
                var url           = $"https://api.qrserver.com/v1/create-qr-code/?size=104x104&data={encoded}";
                var bytes         = await client.GetByteArrayAsync(url);
                using var ms      = new System.IO.MemoryStream(bytes);
                _qrBitmap         = new Bitmap(ms);
                if (!IsDisposed) Invoke((Action)Invalidate);
            }
            catch { /* QR недоступен — ничего */ }
        }

        // ── Spring-pop анимация ────────────────────────────────────────────────
        private const float SpringK     = 0.25f;
        private const float SpringDamp  = 0.65f;
        private const float SpringTarget = 1.0f;

        private void OnSpringTick(object? sender, EventArgs e)
        {
            float force = (SpringTarget - _checkScale) * SpringK;
            _checkVel   = (_checkVel + force) * SpringDamp;
            _checkScale += _checkVel;

            if (Math.Abs(_checkScale - SpringTarget) < 0.005f && Math.Abs(_checkVel) < 0.005f)
            {
                _checkScale = SpringTarget;
                _springTimer.Stop();
            }
            Invalidate(new Rectangle(W / 2 - 28, 28, 56, 56));
        }

        // ── Mouse ─────────────────────────────────────────────────────────────
        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            _hoveredBtn = HitTestButton(e.Location);
            Invalidate();
        }

        private void OnMouseClick(object? sender, MouseEventArgs e)
        {
            int? hit = HitTestButton(e.Location);
            if (hit == null) return;

            switch (hit)
            {
                case 0: // Open
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_publicUrl) { UseShellExecute = true }); }
                    catch { }
                    break;
                case 1: // Markdown
                    Clipboard.SetText($"![]({_publicUrl})");
                    break;
                case 2: // HTML
                    Clipboard.SetText($"<img src=\"{_publicUrl}\" />");
                    break;
                case 3: // QR
                    _showQr = !_showQr;
                    Size = new Size(W, ComputeHeight());
                    ApplyRoundedRegion();
                    Invalidate();
                    break;
                case 10: // Copy URL
                    CopyUrl();
                    break;
                case 11: // Delete — событие наверх
                    Close();
                    break;
                case 12: // Dismiss
                    Close();
                    break;
            }
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && HitTestButton(e.Location) == null)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private int? HitTestButton(Point p)
        {
            // URL bar
            var urlHit = new Rectangle(PadX, _urlBar.Top, W - PadX * 2, 32);
            if (urlHit.Contains(p)) return 10;

            // Grid кнопки
            int gridY = _urlBar.Top + 32 + 16;
            int btnW  = (W - PadX * 2 - 8) / 2;
            for (int i = 0; i < GridButtons.Length; i++)
            {
                int col = i % 2;
                int row = i / 2;
                int bx  = PadX + col * (btnW + 8);
                int by  = gridY + row * (36 + 8);
                if (new Rectangle(bx, by, btnW, 36).Contains(p)) return GridButtons[i].Id;
            }

            // Футер
            int footerY = Height - 48;
            if (new Rectangle(PadX, footerY + 12, 60, 24).Contains(p)) return 11;
            if (new Rectangle(W - PadX - 60, footerY + 12, 60, 24).Contains(p)) return 12;

            return null;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _springTimer.Start();
        }

        private void ApplyRoundedRegion()
        {
            using var path = DrawingHelpers.RoundedRect(
                new RectangleF(0, 0, W, Height), Radius);
            Region = new Region(path);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _springTimer.Dispose();
                _qrBitmap?.Dispose();
            }
            base.Dispose(disposing);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static string FormatSize(long bytes) => bytes switch
        {
            < 1024          => $"{bytes} B",
            < 1024 * 1024   => $"{bytes / 1024} KB",
            _               => $"{bytes / (1024 * 1024.0):F1} MB",
        };
    }
}