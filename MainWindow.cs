using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenSnap
{
    public sealed class MainWindow : Form
    {
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION       = 0x2;

        private const int TitlebarH = AppTheme.TitlebarHeight;
        private const int SidebarW  = AppTheme.SidebarWidth;
        private const int CtrlBtnW  = 36;
        private const int CtrlBtnH  = 28;
        private const int WinRadius = AppTheme.RWindow;

        // Минимальные отступы от краёв до поисковой строки
        private const int SearchMinMargin = 12;
        // Ширина блока правых кнопок: 2 утилиты + 3 win-ctrl + зазор
        private const int RightBlockW = CtrlBtnW * 5 + 8;
        // Ширина левого блока: лого + бренд + отступ
        private const int LeftBlockW  = 18 + 22 + 8 + 120;

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

        private readonly Panel          _appBody;
        private readonly SidebarControl _sidebar;
        private readonly Panel          _mainPane;

        private readonly WorkflowsScreen _workflowsScreen;
        private readonly HistoryScreen   _historyScreen;
        private readonly Panel           _editorPlaceholder;

        private readonly AppSettings _settings;
        private AppScreen _currentScreen = AppScreen.Workflows;
        private bool _isRu = true;

        public MainWindow(AppSettings settings)
        {
            _settings = settings;

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

            // ── Titlebar ──────────────────────────────────────────────────────
            _titlebar = new Panel
            {
                Height    = TitlebarH,
                Dock      = DockStyle.Top,
                BackColor = Color.Transparent,
            };

            _logoBox = new Panel
            {
                Size      = new Size(22, 22),
                BackColor = Color.Transparent,
            };
            _logoBox.Paint += OnLogoPaint;

            _brandLabel = new Label
            {
                Text      = "Auskraft Snap",
                AutoSize  = true,
                BackColor = Color.Transparent,
                Cursor    = Cursors.SizeAll,
            };

            // ── FIX: поисковая строка — ширина динамическая, пересчитывается в LayoutTitlebar
            _searchTrigger = new Panel
            {
                Width     = 340,   // начальное значение, потом пересчитается
                Height    = 26,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand,
            };
            _searchTrigger.Paint      += OnSearchTriggerPaint;
            _searchTrigger.Click      += (_, _) => OnCommandPaletteTrigger();
            _searchTrigger.MouseEnter += (_, _) => _searchTrigger.Invalidate();
            _searchTrigger.MouseLeave += (_, _) => _searchTrigger.Invalidate();

            _btnTheme = new IconButton("🌙", CtrlBtnW, CtrlBtnH);
            _btnTheme.Click += (_, _) => { ThemeManager.Toggle(); ApplyTheme(); };

            _btnLang = new IconButton("RU", CtrlBtnW, CtrlBtnH);
            _btnLang.Click += OnLangToggle;

            _btnMin = new IconButton("─", CtrlBtnW, CtrlBtnH);
            _btnMin.Click += (_, _) => WindowState = FormWindowState.Minimized;

            _btnMax = new IconButton("□", CtrlBtnW, CtrlBtnH);
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

            _sidebar = new SidebarControl();
            _sidebar.ScreenRequested += OnSidebarScreenRequested;

            _mainPane = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent,
            };

            _workflowsScreen = new WorkflowsScreen();
            _historyScreen   = new HistoryScreen();
            _historyScreen.SearchRequested   += (_, _) => OnCommandPaletteTrigger();
            _historyScreen.ItemOpenRequested += OnItemOpenRequested;

            _editorPlaceholder = MakePlaceholder("✏  Editor",
                "Сделай скриншот чтобы открыть его здесь.");

            _historyScreen.LoadFolder(_settings.SaveFolder);

            _mainPane.Controls.Add(_workflowsScreen);
            _mainPane.Controls.Add(_historyScreen);
            _mainPane.Controls.Add(_editorPlaceholder);

            _appBody.Controls.Add(_mainPane);
            _appBody.Controls.Add(_sidebar);

            Controls.Add(_aurora);
            Controls.Add(_appBody);
            Controls.Add(_titlebar);

            _titlebar.MouseDown   += OnTitlebarMouseDown;
            _brandLabel.MouseDown += OnTitlebarMouseDown;

            ThemeManager.ThemeChanged += (_, _) => ApplyTheme();
            ApplyTheme();

            _aurora.SendToBack();
            _aurora.StartAnimation();

            foreach (Control c in _titlebar.Controls)
                 if (c is IconButton btn) btn.ApplyTheme(ThemeManager.Current);

            ShowScreen(AppScreen.Workflows);

            Resize += (_, _) => LayoutTitlebar();
        }

        // ── Screen switching ──────────────────────────────────────────────────
        private void OnSidebarScreenRequested(object? sender, AppScreen screen)
            => ShowScreen(screen);

        public void ShowScreen(AppScreen screen)
        {
            _currentScreen = screen;
            _sidebar.SetActive(screen);

            _workflowsScreen.Visible   = false;
            _historyScreen.Visible     = false;
            _editorPlaceholder.Visible = false;

            switch (screen)
            {
                case AppScreen.Workflows:
                    _workflowsScreen.Visible = true;
                    break;

                case AppScreen.History:
                    _historyScreen.Visible = true;
                    break;

                case AppScreen.Editor:
                    var exts = new[] { ".png", ".jpg", ".jpeg", ".webp" };
                    var last = Directory.Exists(_settings.SaveFolder)
                        ? Directory.GetFiles(_settings.SaveFolder)
                            .Where(f => exts.Contains(
                                Path.GetExtension(f).ToLowerInvariant()))
                            .OrderByDescending(File.GetLastWriteTime)
                            .FirstOrDefault()
                        : null;

                    if (last != null)
                        EditorForm.Open(last, _settings);
                    else
                        _editorPlaceholder.Visible = true;
                    break;

                case AppScreen.Settings:
                    using (var sf = new SettingsForm(_settings))
                    {
                        sf.ShowDialog(this);
                        ShowScreen(_currentScreen == AppScreen.Settings
                            ? AppScreen.Workflows
                            : _currentScreen);
                    }
                    break;
            }

            Invalidate();
        }

        // ── Editor from History tile ──────────────────────────────────────────
        private void OnItemOpenRequested(object? sender, string filePath)
        {
            if (File.Exists(filePath))
                EditorForm.Open(filePath, _settings);
        }

        // ── Lang toggle ───────────────────────────────────────────────────────
        private void OnLangToggle(object? sender, EventArgs e)
        {
            _isRu = !_isRu;
            Loc.Culture = _isRu
                ? new System.Globalization.CultureInfo("ru")
                : System.Globalization.CultureInfo.InvariantCulture;
            _btnLang.SetLabel(_isRu ? "RU" : "EN");
            Invalidate(true);
        }

        // ── Theme ─────────────────────────────────────────────────────────────
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
            if (_btnClose == null) return;

            // ── Левый блок: лого + бренд ──────────────────────────────────────
            int x = 18;
            _logoBox.Location    = new Point(x, (TitlebarH - 22) / 2);
            x += 30;
            _brandLabel.Location = new Point(x, (TitlebarH - _brandLabel.Height) / 2);

            // ── Правый блок: кнопки справа налево ─────────────────────────────
            int rx = Width - 8;
            rx -= CtrlBtnW; _btnClose.Location = new Point(rx, (TitlebarH - CtrlBtnH) / 2);
            rx -= CtrlBtnW; _btnMax.Location   = new Point(rx, (TitlebarH - CtrlBtnH) / 2);
            rx -= CtrlBtnW; _btnMin.Location   = new Point(rx, (TitlebarH - CtrlBtnH) / 2);
            rx -= 8;
            rx -= CtrlBtnW; _btnLang.Location  = new Point(rx, (TitlebarH - CtrlBtnH) / 2);
            rx -= CtrlBtnW; _btnTheme.Location = new Point(rx, (TitlebarH - CtrlBtnH) / 2);

            // ── FIX: поисковая строка занимает пространство между блоками ─────
            // Левый край = конец brandLabel + отступ
            int searchLeft  = _brandLabel.Right + 16;
            // Правый край = начало _btnTheme - отступ
            int searchRight = _btnTheme.Left - 16;
            int searchW     = searchRight - searchLeft;

            // Ограничиваем: не уже 200px и не шире 500px
            searchW = Math.Max(200, Math.Min(500, searchW));

            _searchTrigger.Width    = searchW;
            _searchTrigger.Location = new Point(
                searchLeft + (searchRight - searchLeft - searchW) / 2,
                (TitlebarH - 26) / 2);

            _searchTrigger.Invalidate();
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
            var g = e.Graphics;
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

            // ── FIX: Ctrl+K badge — считаем от правого края панели ────────────
            var kbdText = "Ctrl+K";
            var kbdSize = TextRenderer.MeasureText(kbdText, bodyFont);
            int kbdW    = kbdSize.Width + 10;
            var kbdRect = new Rectangle(
                _searchTrigger.Width - kbdW - 8, 5,
                kbdW, 16);
            DrawingHelpers.DrawGlassCard(g, kbdRect, AppTheme.RXs,
                Color.FromArgb(15, 255, 255, 255), t.Stroke2);
            TextRenderer.DrawText(g, kbdText, bodyFont, kbdRect, t.Text2,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // Плейсхолдер — между иконкой и badge
            var phRect = new Rectangle(28, 5, kbdRect.Left - 32, 16);
            TextRenderer.DrawText(g, "Search, commands, workflows…", bodyFont,
                phRect, t.Text3,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
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