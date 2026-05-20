using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ScreenSnap
{
    public class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private AppSettings settings;
        private HotkeyManager hotkeyManager;
        private YandexDiskService yandex;
        private ToolStripMenuItem yandexMenuItem = new();

        private static Icon LoadIcon()
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.png");
            if (File.Exists(iconPath))
            {
                using var bmp = new Bitmap(iconPath);
                return Icon.FromHandle(bmp.GetHicon());
            }
            return SystemIcons.Application;
        }

        public TrayApplicationContext()
        {
            DotNetEnv.Env.Load();

            settings = AppSettings.Load();
            ScreenCapture.SavePath = settings.SavePath;

            yandex = new YandexDiskService();
            if (!string.IsNullOrEmpty(settings.YandexToken))
                yandex.SetToken(settings.YandexToken);

            hotkeyManager = new HotkeyManager();
            hotkeyManager.FullScreenPressed += () => TakeFullScreen();
            hotkeyManager.RegionPressed += () => TakeRegion();
            hotkeyManager.Register(settings);

            trayIcon = new NotifyIcon()
            {
                Icon = LoadIcon(),
                Text = "Auskraft Snap",
                Visible = true,
                ContextMenuStrip = BuildMenu()
            };
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("📷 Скриншот полного экрана", null, OnFullScreenshot);
            menu.Items.Add("✂️ Выделить область", null, OnRegionScreenshot);
            menu.Items.Add(new ToolStripSeparator());

            yandexMenuItem = new ToolStripMenuItem();
            UpdateYandexMenuItem();
            menu.Items.Add(yandexMenuItem);

            menu.Items.Add("⚙️ Настройки", null, OnSettings);
            menu.Items.Add("❌ Выход", null, OnExit);
            return menu;
        }

        private void UpdateYandexMenuItem()
        {
            if (yandex.IsAuthorized)
            {
                yandexMenuItem.Text = "☁️ Яндекс.Диск подключён ✅";
                yandexMenuItem.Click -= OnYandexConnect;
                yandexMenuItem.Click += OnYandexDisconnect;
            }
            else
            {
                yandexMenuItem.Text = "☁️ Подключить Яндекс.Диск";
                yandexMenuItem.Click -= OnYandexDisconnect;
                yandexMenuItem.Click += OnYandexConnect;
            }
        }

        private async void TakeFullScreen()
        {
            var file = ScreenCapture.CaptureFullScreen();
            if (yandex.IsAuthorized)
                await yandex.UploadAsync(file);
            trayIcon.ShowBalloonTip(3000, "Auskraft Snap", $"Сохранено:\n{file}", ToolTipIcon.Info);
        }

        private async void TakeRegion()
        {
            trayIcon.Visible = false;
            Thread.Sleep(200);
            var file = RegionSelector.CaptureRegion();
            trayIcon.Visible = true;

            if (file != null)
            {
                if (yandex.IsAuthorized)
                    await yandex.UploadAsync(file);
                trayIcon.ShowBalloonTip(3000, "Auskraft Snap", $"Сохранено:\n{file}", ToolTipIcon.Info);
            }
        }

        private void OnFullScreenshot(object? sender, EventArgs e) => TakeFullScreen();
        private void OnRegionScreenshot(object? sender, EventArgs e) => TakeRegion();

        private async void OnYandexConnect(object? sender, EventArgs e)
        {
            await yandex.AuthorizeAsync();
            if (yandex.IsAuthorized)
            {
                UpdateYandexMenuItem();
                trayIcon.ShowBalloonTip(3000, "Auskraft Snap", "Яндекс.Диск подключён!", ToolTipIcon.Info);
            }
        }

        private void OnYandexDisconnect(object? sender, EventArgs e)
        {
            var result = MessageBox.Show("Отключить Яндекс.Диск?", "Auskraft Snap", MessageBoxButtons.YesNo);
            if (result == DialogResult.Yes)
            {
                settings.YandexToken = "";
                settings.Save();
                yandex.SetToken("");
                UpdateYandexMenuItem();
                trayIcon.ShowBalloonTip(3000, "Auskraft Snap", "Яндекс.Диск отключён.", ToolTipIcon.Info);
            }
        }

        private void OnSettings(object? sender, EventArgs e)
        {
            using var form = new SettingsForm(settings);
            if (form.ShowDialog() == DialogResult.OK)
                hotkeyManager.Register(settings);
        }

        private void OnExit(object? sender, EventArgs e)
        {
            hotkeyManager.Dispose();
            trayIcon.Visible = false;
            Application.Exit();
        }
    }
}