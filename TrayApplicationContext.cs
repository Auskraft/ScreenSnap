using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ScreenSnap
{
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon      _trayIcon;
        private readonly HotkeyManager  _hotkeyManager;
        private readonly AppSettings    _settings;
        private readonly WorkflowManager _workflowManager;
        private MainWindow?             _mainWindow;

        public TrayApplicationContext()
        {
            _settings         = AppSettings.Load();
            _hotkeyManager    = new HotkeyManager();
            _workflowManager  = new WorkflowManager(_settings);

            // Pin-событие от WorkflowManager → уведомляем HistoryScreen
            _workflowManager.PinRequested += OnPinRequested;

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

            // Hotkeys — базовые
            _hotkeyManager.FullScreenPressed     += CaptureFullScreen;
            _hotkeyManager.RegionPressed         += CaptureRegion;

            // Hotkeys — пресеты Фазы 3
            _hotkeyManager.QuickSharePressed     += () => RunWorkflowAsync(WorkflowKind.QuickShare);
            _hotkeyManager.DocModePressed        += () => RunWorkflowAsync(WorkflowKind.DocMode);
            _hotkeyManager.PrivacyModePressed    += () => RunWorkflowAsync(WorkflowKind.PrivacyMode);
            _hotkeyManager.SavePinPressed        += () => RunWorkflowAsync(WorkflowKind.SavePin);
            _hotkeyManager.CommandPalettePressed += OpenCommandPalette;

            _hotkeyManager.Register(_settings);

            // Тема из настроек
            if (Enum.TryParse<AppThemeMode>(_settings.Theme, out var mode))
                ThemeManager.SetMode(mode);
            if (Enum.TryParse<AccentPalette>(_settings.Accent, out var accent))
                ThemeManager.SetAccent(accent);
        }

        // ── Context menu ──────────────────────────────────────────────────────
        private ContextMenuStrip BuildContextMenu()
        {
            var menu = new ContextMenuStrip();

            AddItem(menu, "Открыть Auskraft Snap",          () => OpenMainWindow());
            menu.Items.Add(new ToolStripSeparator());

            AddItem(menu, "📸 Скриншот области  Ctrl+Shift+A", () => CaptureRegion());
            AddItem(menu, "⚡ Quick Share  Ctrl+Shift+S",       () => RunWorkflowAsync(WorkflowKind.QuickShare));
            AddItem(menu, "✏️ Documentation Mode  Ctrl+Shift+D", () => RunWorkflowAsync(WorkflowKind.DocMode));
            AddItem(menu, "🛡 Privacy Mode  Ctrl+Shift+P",       () => RunWorkflowAsync(WorkflowKind.PrivacyMode));
            AddItem(menu, "📌 Save & Pin  Ctrl+Shift+T",         () => RunWorkflowAsync(WorkflowKind.SavePin));
            menu.Items.Add(new ToolStripSeparator());

            AddItem(menu, "🌙 Переключить тему", () => ThemeManager.Toggle());
            menu.Items.Add(new ToolStripSeparator());

            AddItem(menu, "⚙️ Настройки", () => new SettingsForm(_settings).ShowDialog());
            menu.Items.Add(new ToolStripSeparator());

            AddItem(menu, "❌ Выход", () => ExitApplication());

            return menu;
        }

        private static void AddItem(ContextMenuStrip menu, string text, Action action)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }

        // ── Window ────────────────────────────────────────────────────────────
        private void OpenMainWindow()
        {
            if (_mainWindow == null || _mainWindow.IsDisposed)
            {
                _mainWindow = new MainWindow(_settings);
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

        // ── Capture (без workflow) ────────────────────────────────────────────
        private void CaptureRegion()
        {
            _mainWindow?.Hide();
            var path = RegionSelector.CaptureRegion();
            if (path != null)
            {
                if (_settings.CopyToClipboard)
                    Clipboard.SetImage(new Bitmap(path));
                _trayIcon.ShowBalloonTip(2000, "Auskraft Snap",
                    $"Сохранено: {Path.GetFileName(path)}", ToolTipIcon.Info);

                NotifyMainWindow(path, pinned: false);
            }
            _mainWindow?.Show();
        }

        private void CaptureFullScreen()
        {
            var path = ScreenCapture.CaptureFullScreen(_settings.SaveFolder);
            if (_settings.CopyToClipboard)
                Clipboard.SetImage(new Bitmap(path));
            _trayIcon.ShowBalloonTip(2000, "Auskraft Snap",
                $"Сохранено: {Path.GetFileName(path)}", ToolTipIcon.Info);

            NotifyMainWindow(path, pinned: false);
        }

        // ── Workflow ──────────────────────────────────────────────────────────
        private enum WorkflowKind { QuickShare, DocMode, PrivacyMode, SavePin }

        private async void RunWorkflowAsync(WorkflowKind kind)
        {
            try
            {
                switch (kind)
                {
                    case WorkflowKind.QuickShare:
                        await _workflowManager.RunQuickShareAsync(_mainWindow);
                        break;
                    case WorkflowKind.DocMode:
                        await _workflowManager.RunDocumentationModeAsync(_mainWindow);
                        break;
                    case WorkflowKind.PrivacyMode:
                        await _workflowManager.RunPrivacyModeAsync(_mainWindow);
                        break;
                    case WorkflowKind.SavePin:
                        await _workflowManager.RunSaveAndPinAsync(_mainWindow);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Tray] Workflow error: {ex}");
            }
        }

        private void OnPinRequested(object? sender, string path)
        {
            NotifyMainWindow(path, pinned: true);
        }

        // ── CommandPalette ────────────────────────────────────────────────────
        private void OpenCommandPalette()
        {
            OpenMainWindow();
            CommandPalette.Open(_mainWindow!);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void NotifyMainWindow(string path, bool pinned)
        {
            if (_mainWindow == null || _mainWindow.IsDisposed) return;
            _mainWindow.Invoke(() =>
            {
                var item = new HistoryScreen.ScreenshotItem
                {
                    FilePath  = path,
                    FileName  = Path.GetFileName(path),
                    Taken     = DateTime.Now,
                    Pinned    = pinned,
                };
                _mainWindow.NotifyNewScreenshot(item);
            });
        }

        // ── Exit ──────────────────────────────────────────────────────────────
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