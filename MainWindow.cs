using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenSnap
{
    /// <summary>
    /// Главное окно Auskraft Snap v2.
    ///
    /// Реализует:
    ///   • Кастомный chrome: FormBorderStyle.None, скруглённые углы 16px
    ///   • Заголовок 44px: логотип, поле «⌘K», кнопки темы/языка/окна
    ///   • Перетаскивание окна за заголовок
    ///   • Aurora-фон (AuroraLayer)
    ///   • Сетка: Sidebar 220px | основная панель (заглушка — будет заменена в Фазе 2)
    ///   • Реакция на ThemeManager.ThemeChanged
    /// </summary>
    public sealed class MainWindow : Form
    {
        // ── Win32 для перетаскивания ─────────────────────────────────────────
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION       = 0x2;

        // ── Layout constants ─────────────────────────────────────────────────
        private const int TitlebarH  = AppTheme.TitlebarHeight; // 44
        private const int SidebarW   = AppTheme.SidebarWidth;   // 220
        private const int CtrlBtnW   = 36;
        private const int CtrlBtnH   = 28;
        private const int WinRadius  = AppTheme.RWindow;        // 16

        // ── Controls ─────────────────────────────────────────────────────────
        private readonly AuroraLayer   _aurora;
        private readonly Panel         _titlebar;
        private readonly Label         _brandLabel;
        private readonly Panel         _logoBox;
        private readonly Panel         _searchTrigger;
        private readonly Label         _searchText;
        private readonly Label         _searchKbd;
        private readonly IconButton    _btnTheme;
        private readonly IconButton    _btnLang;
        private readonly IconButton    _btnMin;
        private readonly IconButton    _btnMax;
        private readonly IconButton    _btnClose;
        private readonly Panel         _appBody;
        private readonly Panel         _sidebar;
        private readonly Panel         _mainPane;

        // ─────────────────────────────────────────────────────────────────────
        public MainWindow()
        {
            // ── Form basics ──────────────────────────────────────────────────
            Text             = "Auskraft Snap";
            FormBorderStyle  = FormBorderStyle.None;
            Size             = new Size(1380, 880);
            MinimumSize      = new Size(1080, 720);
            StartPosition    = FormStartPosition.CenterScreen;
            DoubleBuffered   = true;
            BackColor        = Color.Black;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);

            // ── Aurora (lowest z-order) ──────────────────────────────────────
            _aurora = new AuroraLayer { Dock = DockStyle.Fill };
            Controls.Add(_aurora);

            // ── Titlebar ─────────────────────────────────────────────────────
            _titlebar = new Panel
            {
                Height   = TitlebarH,
                Dock     = DockStyle.Top,
                BackColor= Color.Transparent,
            };
            Controls.Add(_titlebar);

            // Logo box (22×22, accent gradient)
            _logoBox = new Panel
            {
                Size      = new Size(22, 22),
                BackColor = Color.Transparent,
                Cursor    = Cursors.Default,
            };
            _logoBox.Paint += OnLogoPaint;

            // Brand label
            _brandLabel = new Label
            {
                Text      = "Auskraft Snap",
                AutoSize  = true,
                BackColor = Color.Transparent,
                Cursor    = Cursors.SizeAll,
            };

            // ⌘K search trigger
            _searchTrigger = new Panel
            {
                Width     = 340,
                Height    = 26,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand,
            };
            _searchTrigger.Paint     += OnSearchTriggerPaint;
            _searchTrigger.Click     += (_, _) => OnCommandPaletteTrigger();
            _searchTrigger.MouseEnter += (_, _) => _searchTrigger.Invalidate();
            _searchTrigger.MouseLeave += (_, _) => _searchTrigger.Invalidate();

            // Labels inside search (click forwarding — actual text is painted)
            _searchText = new Label { AutoSize = true, BackColor = Color.Transparent };
            _searchKbd  = new Label { AutoSize = true, BackColor = Color.Transparent };

            // Window control buttons
            _btnTheme = new IconButton("🌙", CtrlBtnW, CtrlBtnH);
            _btnTheme.Click += (_, _) => { ThemeManager.Toggle(); ApplyTheme(); };

            _btnLang  = new IconButton("EN", CtrlBtnW, CtrlBtnH);
            // Language toggle wired in Phase 4

            _btnMin   = new IconButton("─", CtrlBtnW, CtrlBtnH);
            _btnMin.Click += (_, _) => WindowState = FormWindowState.Minimized;

            _btnMax   = new IconButton("□", CtrlBtnW, CtrlBtnH);
            _btnMax.Click += (_, _) =>
                WindowState = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal : FormWindowState.Maximized;

            _btnClose = new IconButton("✕", CtrlBtnW, CtrlBtnH, isClose: true);
            _btnClose.Click += (_, _) => Close();

            _titlebar.Controls.AddRange(new Control[]
            {
                _logoBox, _brandLabel, _searchTrigger, _btnTheme, _btnLang,
                _btnMin, _btnMax, _btnClose,
            });

            // ── App body grid ─────────────────────────────────────────────────
            _appBody = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent,
            };

            _sidebar = new Panel
            {
                Width     = SidebarW,
                Dock      = DockStyle.Left,
                BackColor = Color.Transparent,
            };
            _sidebar.Paint += OnSidebarPaint;

            _mainPane = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent,
            };
            _mainPane.Paint += OnMainPanePaint;

            _appBody.Controls.Add(_mainPane);
            _appBody.Controls.Add(_sidebar);
            Controls.Add(_appBody);

            // ── Drag-to-move ──────────────────────────────────────────────────
            _titlebar.MouseDown   += OnTitlebarMouseDown;
            _brandLabel.MouseDown += OnTitlebarMouseDown;

            // ── Theme ─────────────────────────────────────────────────────────
            ThemeManager.ThemeChanged += (_, _) => ApplyTheme();
            ApplyTheme();

            // ── Aurora ────────────────────────────────────────────────────────
            _aurora.SendToBack();
            _aurora.StartAnimation();

            Resize += (_, _) => LayoutTitlebar();
        }

        // ── ApplyTheme ────────────────────────────────────────────────────────
        private void ApplyTheme()
        {
            if (InvokeRequired) { Invoke(ApplyTheme); return; }

            var t = ThemeManager.Current;

            _brandLabel.Font      = t.FontDisplay(FontLoader.BodyM, System.Drawing.FontStyle.Bold);
            _brandLabel.ForeColor = t.Text1;

            foreach (Control c in _titlebar.Controls)
                if (c is IconButton btn) btn.ApplyTheme(t);

            ApplyRoundedRegion();

            Invalidate(true);
            LayoutTitlebar();
        }

        // ── Rounded window region ─────────────────────────────────────────────
        private void ApplyRoundedRegion()
        {
            var path   = DrawingHelpers.RoundedRect(
                new RectangleF(0, 0, Width, Height), WinRadius);
            Region = new Region(path);
        }

        // ── Titlebar layout ───────────────────────────────────────────────────
        private void LayoutTitlebar()
        {
            int x = 18;

            _logoBox.Location    = new Point(x, (TitlebarH - 22) / 2);
            x += 22 + 10;

            _brandLabel.Location = new Point(x, (TitlebarH - _brandLabel.Height) / 2);

            // Centre the search trigger
            _searchTrigger.Location = new Point(
                (Width - _searchTrigger.Width) / 2,
                (TitlebarH - 26) / 2);

            // Right-align controls
            int rx = Width - 12;

            rx -= CtrlBtnW; _btnClose.Location = new Point(rx, (TitlebarH - CtrlBtnH) / 2);
            rx -= CtrlBtnW; _btnMax.Location   = new Point(rx, (TitlebarH - CtrlBtnH) / 2);
            rx -= CtrlBtnW; _btnMin.Location   = new Point(rx, (TitlebarH - CtrlBtnH) / 2);
            rx -= 8;
            rx -= CtrlBtnW; _btnLang.Location  = new Point(rx, (TitlebarH - CtrlBtnH) / 2);
            rx -= CtrlBtnW; _btnTheme.Location = new Point(rx, (TitlebarH - CtrlBtnH) / 2);
        }

        // ── Paint overrides ───────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var t = ThemeManager.Current;

            var rect = new RectangleF(0, 0, Width, Height);
            DrawingHelpers.DrawGlassCard(g, rect, WinRadius, t.BgGlassStrong, t.Stroke2);

            using var pen = new Pen(t.Stroke1, 1f);
            g.DrawLine(pen, 0, TitlebarH, Width, TitlebarH);

            var titleRect = new RectangleF(0, 0, Width, TitlebarH);
            using var tBrush = new LinearGradientBrush(
                titleRect,
                Color.FromArgb(10, 255, 255, 255),
                Color.Transparent,
                LinearGradientMode.Vertical);
            g.FillRectangle(tBrush, titleRect);
        }

        private void OnLogoPaint(object? sender, PaintEventArgs e)
        {
            var g   = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var acc = ThemeManager.Current.Accent;
            var r   = new RectangleF(0, 0, 22, 22);
            DrawingHelpers.DrawAccentFill(g, r, acc, 6);

            using var darkBrush = new SolidBrush(Color.FromArgb(217, 7, 7, 12));
            using var innerPath = DrawingHelpers.RoundedRect(
                new RectangleF(3, 3, 16, 16), 4f);
            g.FillPath(darkBrush, innerPath);

            using var wp = new Pen(Color.White, 1.5f) { LineJoin = LineJoin.Round };
            g.DrawLines(wp, new[] { new PointF(6, 11), new PointF(6, 6), new PointF(11, 6) });

            g.FillEllipse(Brushes.White, 13f, 13f, 5f, 5f);
        }

        private void OnSearchTriggerPaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var t = ThemeManager.Current;

            bool hover = _searchTrigger.ClientRectangle.Contains(
                _searchTrigger.PointToClient(Cursor.Position));

            var fill = hover ? t.Stroke2 : Color.FromArgb(10, 255, 255, 255);
            DrawingHelpers.DrawGlassCard(
                g,
                new RectangleF(0, 0, _searchTrigger.Width, 26),
                AppTheme.RInput,
                fill, t.Stroke1);

            using var iconFont = t.FontBody(11f);
            TextRenderer.DrawText(g, "⌕", iconFont, new Point(10, 5), t.Text3);

            using var bodyFont = t.FontBody(FontLoader.BodyXS);
            TextRenderer.DrawText(g, "Search, commands, workflows…", bodyFont,
                new Point(28, 5), t.Text3);

            var kbdText = "Ctrl+K";
            var kbdSize = TextRenderer.MeasureText(kbdText, bodyFont);
            var kbdRect = new Rectangle(
                _searchTrigger.Width - kbdSize.Width - 16, 5,
                kbdSize.Width + 10, 16);
            DrawingHelpers.DrawGlassCard(
                g, kbdRect, AppTheme.RXs,
                Color.FromArgb(15, 255, 255, 255), t.Stroke2);
            TextRenderer.DrawText(g, kbdText, bodyFont, kbdRect, t.Text2,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void OnSidebarPaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var t = ThemeManager.Current;
            using var pen = new Pen(t.Stroke1, 1f);
            g.DrawLine(pen, SidebarW - 1, 0, SidebarW - 1, _sidebar.Height);
            using var f = t.FontBody(FontLoader.BodyXS);
            TextRenderer.DrawText(g, "Sidebar — Phase 2", f,
                _sidebar.ClientRectangle, t.Text4,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void OnMainPanePaint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var t = ThemeManager.Current;
            using var f = t.FontBody(FontLoader.BodyS);
            TextRenderer.DrawText(g, "Main pane — Phase 2", f,
                _mainPane.ClientRectangle, t.Text4,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // ── Drag-to-move ──────────────────────────────────────────────────────
        private void OnTitlebarMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }

        // ── Command Palette trigger ───────────────────────────────────────────
        public event EventHandler? CommandPaletteRequested;
        private void OnCommandPaletteTrigger()
            => CommandPaletteRequested?.Invoke(this, EventArgs.Empty);

        // ── WndProc — keep rounded region on resize ───────────────────────────
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            const int WM_SIZE = 0x0005;
            if (m.Msg == WM_SIZE) ApplyRoundedRegion();
        }

        // ── Resize handler ────────────────────────────────────────────────────
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutTitlebar();
        }

        // ── Dispose ──────────────────────────────────────────────────────────
        protected override void Dispose(bool disposing)
        {
            if (disposing) _aurora.Dispose();
            base.Dispose(disposing);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  IconButton — simple themed button for the titlebar
    // ═════════════════════════════════════════════════════════════════════════
    internal sealed class IconButton : Button
    {
        private readonly bool _isClose;
        private AppTheme? _theme;

        public IconButton(string text, int w, int h, bool isClose = false)
        {
            Text        = text;
            Size        = new Size(w, h);
            FlatStyle   = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            BackColor   = Color.Transparent;
            Cursor      = Cursors.Hand;
            _isClose    = isClose;

            SetStyle(ControlStyles.Opaque, false);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        }

        public void ApplyTheme(AppTheme t)
        {
            _theme    = t;
            Font      = t.FontBody(FontLoader.BodyXS);
            ForeColor = t.Text2;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool hover = ClientRectangle.Contains(PointToClient(Cursor.Position));

            if (hover)
            {
                Color hoverBg = _isClose
                    ? Color.FromArgb(220, 0xE8, 0x11, 0x23)
                    : Color.FromArgb(20, 255, 255, 255);

                using var brush = new SolidBrush(hoverBg);
                using var path  = DrawingHelpers.RoundedRect(
                    new RectangleF(0, 0, Width, Height), 6f);
                g.FillPath(brush, path);
            }

            var textColor = (_isClose && hover) ? Color.White : (_theme?.Text2 ?? ForeColor);
            TextRenderer.DrawText(g, Text, Font ?? SystemFonts.DefaultFont,
                ClientRectangle, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}