using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenSnap.Overlays
{
    // ═══════════════════════════════════════════════════════════════════════
    //  PinOverlay — плавающее окно поверх всех окон OS
    //
    //  Фичи:
    //    • Всегда поверх (TopMost)
    //    • Drag-to-move за любую точку изображения
    //    • Opacity slider (полоска снизу)
    //    • Кнопка закрытия (×) в углу
    //    • Двойной клик → открыть в EditorForm
    //
    //  Использование:
    //    PinOverlay.Show(imagePath, settings);
    // ═══════════════════════════════════════════════════════════════════════
    public sealed class PinOverlay : Form
    {
        // ── Win32 ─────────────────────────────────────────────────────────────
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE    = 0x0002;
        private const uint SWP_NOSIZE    = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        // ── Layout ────────────────────────────────────────────────────────────
        private const int SliderH   = 28;
        private const int CloseSize = 24;
        private const int MinW      = 120;
        private const int MinH      = 80;
        private const int MaxW      = 900;
        private const int MaxH      = 700;
        private const int Radius    = 10;

        // ── State ─────────────────────────────────────────────────────────────
        private readonly AppSettings _settings;
        private readonly string      _imagePath;
        private Bitmap?              _source;

        // Opacity slider
        private float  _opacity      = 1.0f;
        private bool   _sliderDrag   = false;
        private int    _sliderStartX = 0;

        // Resize grip
        private bool  _resizing     = false;
        private Point _resizeStart;
        private Size  _resizeOrigin;

        // ── Controls ──────────────────────────────────────────────────────────
        private readonly Panel  _imagePanel;
        private readonly Panel  _sliderBar;
        private readonly Button _btnClose;

        // ─────────────────────────────────────────────────────────────────────
        private PinOverlay(string imagePath, AppSettings settings)
        {
            _imagePath = imagePath;
            _settings  = settings;

            try { _source = new Bitmap(imagePath); }
            catch { _source = null; }

            // ── Form ──────────────────────────────────────────────────────────
            FormBorderStyle = FormBorderStyle.None;
            TopMost         = true;
            ShowInTaskbar   = false;
            StartPosition   = FormStartPosition.Manual;
            BackColor       = Color.Black;
            DoubleBuffered  = true;
            Opacity         = 1.0;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);

            // Вычисляем начальный размер — вписываем в MaxW×MaxH
            var (initW, initH) = CalcInitialSize();
            Size     = new Size(initW, initH + SliderH);
            Location = new Point(
                Screen.PrimaryScreen!.WorkingArea.Right  - initW  - 24,
                Screen.PrimaryScreen!.WorkingArea.Bottom - initH - SliderH - 24);

            // ── Image panel ───────────────────────────────────────────────────
            _imagePanel = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent,
                Cursor    = Cursors.SizeAll,
            };
            _imagePanel.Paint      += DrawImage;
            _imagePanel.MouseDown  += OnImageMouseDown;
            _imagePanel.MouseMove  += OnImageMouseMove;
            _imagePanel.MouseUp    += OnImageMouseUp;
            _imagePanel.DoubleClick += OnImageDoubleClick;

            // ── Close button ──────────────────────────────────────────────────
            _btnClose = new Button
            {
                Size      = new Size(CloseSize, CloseSize),
                Location  = new Point(initW - CloseSize - 4, 4),
                Text      = "×",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(180, 20, 20, 30),
                ForeColor = Color.FromArgb(220, 255, 80, 80),
                Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
                Cursor    = Cursors.Hand,
                TabStop   = false,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right,
            };
            _btnClose.FlatAppearance.BorderSize         = 0;
            _btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 255, 60, 60);
            _btnClose.Click += (_, _) => Close();

            // ── Slider bar ────────────────────────────────────────────────────
            _sliderBar = new Panel
            {
                Height    = SliderH,
                Dock      = DockStyle.Bottom,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand,
            };
            _sliderBar.Paint      += DrawSlider;
            _sliderBar.MouseDown  += OnSliderMouseDown;
            _sliderBar.MouseMove  += OnSliderMouseMove;
            _sliderBar.MouseUp    += OnSliderMouseUp;

            Controls.Add(_imagePanel);
            Controls.Add(_sliderBar);
            _imagePanel.Controls.Add(_btnClose);

            ApplyRoundedRegion();

            // Гарантируем TopMost через Win32
            SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

            Resize += (_, _) =>
            {
                _btnClose.Location = new Point(Width - CloseSize - 4, 4);
                ApplyRoundedRegion();
            };
        }

        // ── Public ────────────────────────────────────────────────────────────
        public static void Show(string imagePath, AppSettings settings)
        {
            var overlay = new PinOverlay(imagePath, settings);
            overlay.Show();
        }

        // ── Draw: Image ───────────────────────────────────────────────────────
        private void DrawImage(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            // Фон
            g.Clear(Color.FromArgb(18, 18, 28));

            if (_source == null)
            {
                using var f = new Font("Segoe UI", 10f);
                TextRenderer.DrawText(g, "Нет изображения", f,
                    _imagePanel.ClientRectangle, Color.Gray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            // Вписываем с паддингом 6px
            int pad = 6;
            float scale = Math.Min(
                (float)(_imagePanel.Width  - pad * 2) / _source.Width,
                (float)(_imagePanel.Height - pad * 2) / _source.Height);
            int dw = (int)(_source.Width  * scale);
            int dh = (int)(_source.Height * scale);
            var imgRect = new Rectangle(
                pad + (_imagePanel.Width  - pad * 2 - dw) / 2,
                pad + (_imagePanel.Height - pad * 2 - dh) / 2,
                dw, dh);

            g.DrawImage(_source, imgRect);

            // Тонкая рамка вокруг скриншота
            using var pen = new Pen(Color.FromArgb(40, 255, 255, 255), 1f);
            g.DrawRectangle(pen, imgRect.X - 1, imgRect.Y - 1,
                imgRect.Width + 1, imgRect.Height + 1);

            // Resize grip (правый нижний угол)
            DrawResizeGrip(g);
        }

        private void DrawResizeGrip(Graphics g)
        {
            int x = _imagePanel.Width  - 14;
            int y = _imagePanel.Height - 14;
            using var pen = new Pen(Color.FromArgb(80, 255, 255, 255), 1.2f);
            for (int i = 0; i < 3; i++)
            {
                int d = i * 4;
                g.DrawLine(pen, x + d, y + 10, x + 10, y + d);
            }
        }

        // ── Draw: Slider ──────────────────────────────────────────────────────
        private void DrawSlider(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var r = new RectangleF(0, 0, _sliderBar.Width, SliderH);

            // Фон полоски
            using var bgBrush = new SolidBrush(Color.FromArgb(200, 14, 14, 22));
            g.FillRectangle(bgBrush, r);

            // Метка
            using var labelFont = new Font("Segoe UI", 8f);
            TextRenderer.DrawText(g, "Opacity", labelFont,
                new Rectangle(8, 0, 52, SliderH), Color.FromArgb(120, 200, 200, 220),
                TextFormatFlags.VerticalCenter);

            // Трек
            int trackX = 62;
            int trackW = _sliderBar.Width - trackX - 44;
            int trackY = SliderH / 2 - 2;
            var trackR = new RectangleF(trackX, trackY, trackW, 4);

            using var trackBg = new SolidBrush(Color.FromArgb(50, 200, 200, 255));
            g.FillRectangle(trackBg, trackR);

            // Заполненная часть
            float fillW = trackW * _opacity;
            if (fillW > 0)
            {
                using var fillBrush = new LinearGradientBrush(
                    new RectangleF(trackX, trackY, Math.Max(fillW, 1), 4),
                    Color.FromArgb(110, 75, 255),
                    Color.FromArgb(0, 212, 255),
                    LinearGradientMode.Horizontal);
                g.FillRectangle(fillBrush, trackX, trackY, fillW, 4);
            }

            // Ручка
            float thumbX = trackX + fillW - 6;
            var thumbR = new RectangleF(thumbX, SliderH / 2f - 6, 12, 12);
            using var thumbFill = new SolidBrush(Color.White);
            g.FillEllipse(thumbFill, thumbR);
            using var thumbBorder = new Pen(Color.FromArgb(110, 75, 255), 1.5f);
            g.DrawEllipse(thumbBorder, thumbR);

            // Процент
            using var pctFont = new Font("Segoe UI", 8f, FontStyle.Bold);
            TextRenderer.DrawText(g, $"{(int)(_opacity * 100)}%", pctFont,
                new Rectangle(_sliderBar.Width - 40, 0, 36, SliderH),
                Color.FromArgb(180, 220, 220, 255),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        // ── Drag to move ──────────────────────────────────────────────────────
        private void OnImageMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            // Resize grip зона (правый нижний угол 20×20)
            if (e.X > _imagePanel.Width - 20 && e.Y > _imagePanel.Height - 20)
            {
                _resizing     = true;
                _resizeStart  = Cursor.Position;
                _resizeOrigin = Size;
                return;
            }

            ReleaseCapture();
            SendMessage(Handle, 0xA1, 0x2, 0);
        }

        private void OnImageMouseMove(object? sender, MouseEventArgs e)
        {
            if (!_resizing) return;
            var delta = new Point(
                Cursor.Position.X - _resizeStart.X,
                Cursor.Position.Y - _resizeStart.Y);

            int newW = Math.Max(MinW, Math.Min(MaxW, _resizeOrigin.Width  + delta.X));
            int newH = Math.Max(MinH, Math.Min(MaxH, _resizeOrigin.Height + delta.Y));
            Size = new Size(newW, newH);
            Invalidate(true);
        }

        private void OnImageMouseUp(object? sender, MouseEventArgs e)
        {
            _resizing = false;
        }

        private void OnImageDoubleClick(object? sender, EventArgs e)
        {
            EditorForm.Open(_imagePath, _settings);
        }

        // ── Opacity slider interaction ─────────────────────────────────────────
        private void OnSliderMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _sliderDrag   = true;
            _sliderStartX = e.X;
            UpdateOpacity(e.X);
        }

        private void OnSliderMouseMove(object? sender, MouseEventArgs e)
        {
            if (!_sliderDrag) return;
            UpdateOpacity(e.X);
        }

        private void OnSliderMouseUp(object? sender, MouseEventArgs e)
        {
            _sliderDrag = false;
        }

        private void UpdateOpacity(int mouseX)
        {
            int trackX = 62;
            int trackW = _sliderBar.Width - trackX - 44;
            float t = Math.Max(0f, Math.Min(1f, (float)(mouseX - trackX) / trackW));
            // Минимум 10% чтобы окно не пропало совсем
            _opacity = Math.Max(0.1f, t);
            Opacity  = _opacity;
            _sliderBar.Invalidate();
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private (int w, int h) CalcInitialSize()
        {
            if (_source == null) return (320, 200);
            float scale = Math.Min(
                (float)MaxW / _source.Width,
                (float)MaxH / _source.Height);
            // Не увеличиваем маленькие скриншоты больше оригинала
            scale = Math.Min(scale, 1.0f);
            return ((int)(_source.Width * scale), (int)(_source.Height * scale));
        }

        private void ApplyRoundedRegion()
        {
            using var path = DrawingHelpers.RoundedRect(
                new RectangleF(0, 0, Width, Height), Radius);
            Region = new Region(path);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _source?.Dispose();
            base.Dispose(disposing);
        }
    }
}