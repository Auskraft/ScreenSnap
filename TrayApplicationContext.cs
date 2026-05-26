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

        // ── Repeat last ───────────────────────────────────────────────────────
        private enum LastAction { None, FullScreen, Region, ActiveWindow }
        private LastAction _lastAction = LastAction.None;

        public TrayApplicationContext()
        {
            _settings         = AppSettings.Load();
            _hotkeyManager    = new HotkeyManager();
            _workflowManager  = new WorkflowManager(_settings);

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

            // Hotkeys
            _hotkeyManager.FullScreenPressed     += CaptureFullScreen;
            _hotkeyManager.RegionPressed         += CaptureRegion;
            _hotkeyManager.QuickSharePressed     += () => RunWorkflowAsync(WorkflowKind.QuickShare);
            _hotkeyManager.DocModePressed        += () => RunWorkflowAsync(WorkflowKind.DocMode);
            _hotkeyManager.PrivacyModePressed    += () => RunWorkflowAsync(WorkflowKind.PrivacyMode);
            _hotkeyManager.SavePinPressed        += () => RunWorkflowAsync(WorkflowKind.SavePin);
            _hotkeyManager.CommandPalettePressed += OpenCommandPalette;
            _hotkeyManager.ActiveWindowPressed   += CaptureActiveWindow;
            _hotkeyManager.RepeatLastPressed     += RepeatLast;

            _hotkeyManager.Register(_settings);

            // Тема
            if (Enum.TryParse<AppThemeMode>(_settings.Theme, out var mode))
                ThemeManager.SetMode(mode);
            if (Enum.TryParse<AccentPalette>(_settings.Accent, out var accent))
                ThemeManager.SetAccent(accent);

            // Онбординг при первом запуске
            OnboardingForm.ShowIfNeeded(_settings);
        }

        // ── Context menu ──────────────────────────────────────────────────────
        private ContextMenuStrip BuildContextMenu()
        {
            var menu = new ContextMenuStrip();

            AddItem(menu, "Открыть Auskraft Snap",                () => OpenMainWindow());
            menu.Items.Add(new ToolStripSeparator());

            AddItem(menu, "Скриншот области  Ctrl+Shift+A",      () => CaptureRegion());
            AddItem(menu, "Активное окно  Ctrl+Shift+W",          () => CaptureActiveWindow());
            AddItem(menu, "Quick Share  Ctrl+Shift+S",            () => RunWorkflowAsync(WorkflowKind.QuickShare));
            AddItem(menu, "Documentation Mode  Ctrl+Shift+D",     () => RunWorkflowAsync(WorkflowKind.DocMode));
            AddItem(menu, "Privacy Mode  Ctrl+Shift+P",           () => RunWorkflowAsync(WorkflowKind.PrivacyMode));
            AddItem(menu, "Save & Pin  Ctrl+Shift+T",             () => RunWorkflowAsync(WorkflowKind.SavePin));
            AddItem(menu, "Повторить последнее  Ctrl+Shift+R",    () => RepeatLast());
            menu.Items.Add(new ToolStripSeparator());

            AddItem(menu, "Переключить тему",                     () => ThemeManager.Toggle());
            menu.Items.Add(new ToolStripSeparator());

            AddItem(menu, "Настройки",                            () => new SettingsForm(_settings).ShowDialog());
            AddItem(menu, "Онбординг",                            () => OnboardingForm.ShowIfNeeded(_settings, force: true));
            menu.Items.Add(new ToolStripSeparator());

            AddItem(menu, "Выход",                                () => ExitApplication());

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

        // ── Capture ───────────────────────────────────────────────────────────
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
                _lastAction = LastAction.Region;
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
            _lastAction = LastAction.FullScreen;
        }

        private void CaptureActiveWindow()
        {
            System.Threading.Tasks.Task.Delay(150).ContinueWith(_ =>
            {
                var bmp = HotkeyManager.CaptureActiveWindow();
                if (bmp == null) return;

                var folder = _settings.SaveFolder;
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var fileName = $"window_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
                var path     = Path.Combine(folder, fileName);
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                bmp.Dispose();

                if (_settings.CopyToClipboard)
                    Clipboard.SetImage(new Bitmap(path));

                _trayIcon.ShowBalloonTip(2000, "Auskraft Snap",
                    $"Окно захвачено: {fileName}", ToolTipIcon.Info);

                NotifyMainWindow(path, pinned: false);
                _lastAction = LastAction.ActiveWindow;
            }, System.Threading.Tasks.TaskScheduler.Default);
        }

        private void RepeatLast()
        {
            switch (_lastAction)
            {
                case LastAction.FullScreen:    CaptureFullScreen();   break;
                case LastAction.Region:        CaptureRegion();       break;
                case LastAction.ActiveWindow:  CaptureActiveWindow(); break;
                case LastAction.None:
                    _trayIcon.ShowBalloonTip(1500, "Auskraft Snap",
                        "Нет предыдущего действия", ToolTipIcon.Info);
                    break;
            }
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
            // Показываем floating overlay поверх всех окон
            Overlays.PinOverlay.Show(path, _settings);
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