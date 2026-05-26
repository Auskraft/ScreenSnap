using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ScreenSnap
{
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon    _trayIcon;
        private readonly HotkeyManager _hotkeyManager;
        private readonly AppSettings   _settings;
        private MainWindow?            _mainWindow;

        public TrayApplicationContext()
        {
            _settings      = AppSettings.Load();
            _hotkeyManager = new HotkeyManager();

            // Tray icon
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.png");
            Icon trayIconImage;
            if (File.Exists(iconPath))
            {
                using var bmp = new Bitmap(iconPath);
                trayIconImage = Icon.FromHandle(bmp.GetHicon());
            }
            else
            {
                trayIconImage = SystemIcons.Application;
            }

            _trayIcon = new NotifyIcon
            {
                Icon             = trayIconImage,
                Text             = "Auskraft Snap",
                Visible          = true,
                ContextMenuStrip = BuildContextMenu(),
            };
            _trayIcon.DoubleClick += (_, _) => OpenMainWindow();

            // Hotkeys
            _hotkeyManager.FullScreenPressed += CaptureFullScreen;
            _hotkeyManager.RegionPressed     += CaptureRegion;
            _hotkeyManager.Register(_settings);

            // Тема из настроек
            if (Enum.TryParse<AppThemeMode>(_settings.Theme, out var mode))
                ThemeManager.SetMode(mode);
            if (Enum.TryParse<AccentPalette>(_settings.Accent, out var accent))
                ThemeManager.SetAccent(accent);
        }

        private ContextMenuStrip BuildContextMenu()
        {
            var menu = new ContextMenuStrip();

            var open = new ToolStripMenuItem("Открыть Auskraft Snap");
            open.Click += (_, _) => OpenMainWindow();
            menu.Items.Add(open);

            menu.Items.Add(new ToolStripSeparator());

            var capture = new ToolStripMenuItem("📸 Скриншот области  Ctrl+Shift+A");
            capture.Click += (_, _) => CaptureRegion();
            menu.Items.Add(capture);

            var quickShare = new ToolStripMenuItem("⚡ Quick Share  Ctrl+Shift+S");
            quickShare.Click += (_, _) => RunQuickShare();
            menu.Items.Add(quickShare);

            menu.Items.Add(new ToolStripSeparator());

            var themeToggle = new ToolStripMenuItem("🌙 Переключить тему");
            themeToggle.Click += (_, _) => ThemeManager.Toggle();
            menu.Items.Add(themeToggle);

            menu.Items.Add(new ToolStripSeparator());

            var settings = new ToolStripMenuItem("⚙️ Настройки");
            settings.Click += (_, _) => new SettingsForm(_settings).ShowDialog();
            menu.Items.Add(settings);

            menu.Items.Add(new ToolStripSeparator());

            var exit = new ToolStripMenuItem("❌ Выход");
            exit.Click += (_, _) => ExitApplication();
            menu.Items.Add(exit);

            return menu;
        }

        private void OpenMainWindow()
        {
            if (_mainWindow == null || _mainWindow.IsDisposed)
            {
                _mainWindow = new MainWindow();
                _mainWindow.CommandPaletteRequested += (_, _) => OpenCommandPalette();
                _mainWindow.FormClosed += (_, _) => _mainWindow = null;
                _mainWindow.Show();
            }
            else
            {
                _mainWindow.BringToFront();
                _mainWindow.Activate();
            }
        }

        private void CaptureRegion()
        {
            _mainWindow?.Hide();
            var path = RegionSelector.CaptureRegion();
            if (path != null)
            {
                if (_settings.CopyToClipboard)
                    Clipboard.SetImage(new Bitmap(path));
                _trayIcon.ShowBalloonTip(2000, "Auskraft Snap", $"Сохранено: {Path.GetFileName(path)}", ToolTipIcon.Info);
            }
            _mainWindow?.Show();
        }

        private void CaptureFullScreen()
        {
            var path = ScreenCapture.CaptureFullScreen();
            if (_settings.CopyToClipboard)
                Clipboard.SetImage(new Bitmap(path));
            _trayIcon.ShowBalloonTip(2000, "Auskraft Snap", $"Сохранено: {Path.GetFileName(path)}", ToolTipIcon.Info);
        }

        private void RunQuickShare()
        {
            // Фаза 3 — WorkflowManager
            _trayIcon.ShowBalloonTip(2000, "Auskraft Snap",
                "⚡ Quick Share — будет готов в Фазе 3", ToolTipIcon.Info);
        }

        private void OpenCommandPalette()
        {
            OpenMainWindow();
            // Фаза 3 — CommandPalette.cs
        }

        private void ExitApplication()
        {
            _hotkeyManager.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hotkeyManager.Dispose();
                _trayIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}