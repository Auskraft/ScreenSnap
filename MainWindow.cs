using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenSnap
{
    /// <summary>
    /// Главное окно Auskraft Snap v2 — Фаза 2.
    ///
    /// Изменения vs Фаза 1:
    ///   • _sidebar заменён на SidebarControl — полная навигация
    ///   • _mainPane содержит WorkflowsScreen / HistoryScreen / EditorPlaceholder / SettingsForm
    ///   • Переключение экранов по событию SidebarControl.ScreenRequested
    ///   • CommandPaletteRequested пробрасывается из _sidebar и searchTrigger
    /// </summary>
    public sealed class MainWindow : Form
    {
        // ── Win32 ────────────────────────────────────────────────────────────
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION       = 0x2;

        // ── Layout ───────────────────────────────────────────────────────────
        private const int TitlebarH = AppTheme.TitlebarHeight; // 44
        private const int SidebarW  = AppTheme.SidebarWidth;   // 220
        private const int CtrlBtnW  = 36;
        private const int CtrlBtnH  = 28;
        private const int WinRadius = AppTheme.RWindow;        // 16

        // ── Titlebar controls ─────────────────────────────────────────────────
        private readonly AuroraLayer   _aurora;
        private readonly Panel         _titlebar;
        private readonly Label         _brandLabel;
        private readonly Panel         _logoBox;
        private readonly Panel         _searchTrigger;
        private readonly IconButton    _btnTheme;
        private readonly IconButton    _btnLang;
        private readonly IconButton    _btnMin;
        private readonly IconButton    _btnMax;
        private readonly IconButton    _btnClose;

        // ── App body ──────────────────────────────────────────────────────────
        private readonly Panel          _appBody;
        private readonly SidebarControl _sidebar;
        private readonly Panel          _mainPane;

        // ── Screens (Phase 2) ─────────────────────────────────────────────────
        private readonly WorkflowsScreen _workflowsScreen;
        private readonly HistoryScreen   _historyScreen;
        private readonly Panel           _editorPlaceholder;

        // Settings открывается как модальное окно (отдельная форма)
        private readonly AppSettings _settings;

        // ── Current screen ────────────────────────────────────────────────────
        private AppScreen _currentScreen = AppScreen.Workflows;

        // ─────────────────────────────────────────────────────────────────────
        public MainWindow(AppSettings settings)
        {
            _settings = settings;

            // ── Form ─────────────────────────────────────────────────────────
            Text            = "Auskraft Snap";
            FormBorderStyle = FormBorderStyle.None;
            Size            = new Size(1380, 880);
            MinimumSize     = new Size(1080, 720);
            StartPosition   = FormStartPosition.CenterScreen;
            DoubleBuffered  = true;
            BackColor       = Color.Black;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);

            // ── Aurora ────────────────────────────────────────────────────────
            _aurora = new AuroraLayer { Dock = DockStyle.Fill };
            Controls.Add(_aurora);

            // ── Titlebar ──────────────────────────────────────────────────────
            _titlebar = new Panel
            {
                Height    = TitlebarH,
                Dock      = DockStyle.Top,
                BackColor = Color.Transparent,
            };
            Controls.Add(_titlebar);

            // Logo box
            _logoBox = new Panel
            {
                Size      = new Size(22, 22),
                BackColor = Color.Transparent,
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

            // Search trigger → Command Palette
            _searchTrigger = new Panel
            {
                Width     = 340,
                Height    = 26,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand,
            };
            _searchTrigger.Paint      += OnSearchTriggerPaint;
            _searchTrigger.Click      += (_, _) => OnCommandPaletteTrigger();
            _searchTrigger.MouseEnter += (_, _) => _searchTrigger.Invalidate();
            _searchTrigger.MouseLeave += (_, _) => _searchTrigger.Invalidate();

            // Window control buttons
            _btnTheme = new IconButton("🌙", CtrlBtnW, CtrlBtnH);
            _btnTheme.Click += (_, _) => { ThemeManager.Toggle(); ApplyTheme(); };

            _btnLang  = new IconButton("EN", CtrlBtnW, CtrlBtnH);

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
                _logoBox, _brandLabel, _searchTrigger,
                _btnTheme, _btnLang, _btnMin, _btnMax, _btnClose,
            });

            // ── App body ──────────────────────────────────────────────────────
            _appBody = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent,
            };

            // Sidebar (Phase 2 — полная навигация)
            _sidebar = new SidebarControl();
            _sidebar.ScreenRequested += OnSidebarScreenRequested;

            // Main pane — контейнер для экранов
            _mainPane = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent,
            };

            // Screens
            _workflowsScreen = new WorkflowsScreen();
            _historyScreen   = new HistoryScreen();
            _historyScreen.SearchRequested += (_, _) => OnCommandPaletteTrigger();

            _editorPlaceholder = MakePlaceholder("✏️  Editor", "Появится в Фазе 4.");

            // Загружаем историю из папки настроек
            _historyScreen.LoadFolder(_settings.SaveFolder);

            // Добавляем все экраны в mainPane (скрытые)
            _mainPane.Controls.Add(_workflowsScreen);
            _mainPane.Controls.Add(_historyScreen);
            _mainPane.Controls.Add(_editorPlaceholder);

            _appBody.Controls.Add(_mainPane);
            _appBody.Controls.Add(_sidebar);
            Controls.Add(_appBody);

            // ── Drag ──────────────────────────────────────────────────────────
            _titlebar.MouseDown   += OnTitlebarMouseDown;
            _brandLabel.MouseDown += OnTitlebarMouseDown;

            // ── Theme ─────────────────────────────────────────────────────────
            ThemeManager.ThemeChanged += (_, _) => ApplyTheme();
            ApplyTheme();

            // ── Aurora ────────────────────────────────────────────────────────
            _aurora.SendToBack();
            _aurora.StartAnimation();

            // Показываем стартовый экран
            ShowScreen(AppScreen.Workflows);

            Resize += (_, _) => LayoutTitlebar();
        }

        // ── Screen switching ──────────────────────────────────────────────────
        private void OnSidebarScreenRequested(object? sender, AppScreen screen)
        {
            ShowScreen(screen);
        }

        public void ShowScreen(AppScreen screen)
        {
            _currentScreen = screen;
            _sidebar.SetActive(screen);

            // Скрываем все
            _workflowsScreen.Visible    = false;
            _historyScreen.Visible      = false;
            _editorPlaceholder.Visible  = false;

            switch (screen)
            {
                case AppScreen.Workflows:
                    _workflowsScreen.Visible = true;
                    break;

                case AppScreen.History:
                    _historyScreen.Visible = true;
                    break;

                case AppScreen.Editor:
                    _editorPlaceholder.Visible = true;
                    break;

                case AppScreen.Settings:
                    // Settings — модальная форма поверх
                    using (var sf = new SettingsForm(_settings))
                    {
                        sf.ShowDialog(this);
                        // После закрытия settings — остаёмся на предыдущем экране
                        ShowScreen(_currentScreen == AppScreen.Settings
                            ? AppScreen.Workflows
                            : _currentScreen);
                    }
                    break;
            }

            Invalidate();
        }

        // ── Titlebar ──────────────────────────────────────────────────────────
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

        private void ApplyRoundedRegion()
        {
            using var path = DrawingHelpers.RoundedRect(
                new RectangleF(0, 0, Width, Height), WinRadius);
            Region = new Region(path);
        }

        private void LayoutTitlebar()
        {
            // Защита от вызова до инициализации контролов
            if (_btnClose == null) return;

            int x = 18;
            _logoBox.Location    = new Point(x, (TitlebarH - 22) / 2);
            x += 32;
            _brandLabel.Location = new Point(x, (TitlebarH - _brandLabel.Height) / 2);

            _searchTrigger.Location = new Point(
                (Width - _searchTrigger.Width) / 2,
                (TitlebarH - 26) / 2);

            int rx = Width - 12;
            rx -= CtrlBtnW; _btnClose.Location = new Point(rx, (TitlebarH - CtrlBtnH) / 2);
            rx -= CtrlBtnW; _btnMax.Location   = new Point(rx, (TitlebarH - CtrlBtnH) / 2);
            rx -= CtrlBtnW; _btnMin.Location   = new Point(rx, (TitlebarH - CtrlBtnH) / 2);
            rx -= 8;
            rx -= CtrlBtnW; _btnLang.Location  = new Point(rx, (TitlebarH - CtrlBtnH) / 2);
            rx -= CtrlBtnW; _btnTheme.Location = new Point(rx, (TitlebarH - CtrlBtnH) / 2);
        }

        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var t = ThemeManager.Current;

            DrawingHelpers.DrawGlassCard(g,
                new RectangleF(0, 0, Width, Height), WinRadius,
                t.BgGlassStrong, t.Stroke2);

            using var pen = new Pen(t.Stroke1, 1f);
            g.DrawLine(pen, 0, TitlebarH, Width, TitlebarH);

            using var tBrush = new LinearGradientBrush(
                new RectangleF(0, 0, Width, TitlebarH),
                Color.FromArgb(10, 255, 255, 255), Color.Transparent,
                LinearGradientMode.Vertical);
            g.FillRectangle(tBrush, new RectangleF(0, 0, Width, TitlebarH));
        }

        private void OnLogoPaint(object? sender, PaintEventArgs e)
        {
            var g   = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var acc = ThemeManager.Current.Accent;
            DrawingHelpers.DrawAccentFill(g, new RectangleF(0, 0, 22, 22), acc, 6);
            using var darkBrush = new SolidBrush(Color.FromArgb(217, 7, 7, 12));
            using var inner = DrawingHelpers.RoundedRect(new RectangleF(3, 3, 16, 16), 4f);
            g.FillPath(darkBrush, inner);
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

            DrawingHelpers.DrawGlassCard(g,
                new RectangleF(0, 0, _searchTrigger.Width, 26),
                AppTheme.RInput,
                hover ? t.Stroke2 : Color.FromArgb(10, 255, 255, 255), t.Stroke1);

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
            DrawingHelpers.DrawGlassCard(g, kbdRect, AppTheme.RXs,
                Color.FromArgb(15, 255, 255, 255), t.Stroke2);
            TextRenderer.DrawText(g, kbdText, bodyFont, kbdRect, t.Text2,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // ── Drag ──────────────────────────────────────────────────────────────
        private void OnTitlebarMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }

        // ── Command Palette ───────────────────────────────────────────────────
        public event EventHandler? CommandPaletteRequested;

        private void OnCommandPaletteTrigger()
        {
            CommandPaletteRequested?.Invoke(this, EventArgs.Empty);
            CommandPalette.Open(this);
        }
        // ── Helpers ───────────────────────────────────────────────────────────
        private Panel MakePlaceholder(string icon, string message)
        {
            var p = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent,
                Visible   = false,
            };
            p.Paint += (_, e) =>
            {
                var t = ThemeManager.Current;
                using var f1 = t.FontDisplay(32f);
                TextRenderer.DrawText(e.Graphics, icon, f1,
                    new Rectangle(0, p.Height / 2 - 60, p.Width, 48), t.Text4,
                    TextFormatFlags.HorizontalCenter);
                using var f2 = t.FontBody(FontLoader.BodyS);
                TextRenderer.DrawText(e.Graphics, message, f2,
                    new Rectangle(0, p.Height / 2 - 8, p.Width, 28), t.Text4,
                    TextFormatFlags.HorizontalCenter);
            };
            return p;
        }

        // ── Public: уведомить HistoryScreen о новом снимке ────────────────────
        public void NotifyNewScreenshot(HistoryScreen.ScreenshotItem item)
        {
            _historyScreen.AddItem(item);
            _sidebar.SetBadge(AppScreen.History, _sidebar.GetBadge(AppScreen.History) + 1);
        }

        // ── WndProc ───────────────────────────────────────────────────────────
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            const int WM_SIZE = 0x0005;
            if (m.Msg == WM_SIZE) ApplyRoundedRegion();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_btnClose == null) return;
            LayoutTitlebar();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _aurora.Dispose();
            base.Dispose(disposing);
        }
    }
}