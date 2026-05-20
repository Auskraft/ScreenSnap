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

        public TrayApplicationContext()
        {
            settings = AppSettings.Load();
            ScreenCapture.SavePath = settings.SavePath;

            hotkeyManager = new HotkeyManager();
            hotkeyManager.FullScreenPressed += () => TakeFullScreen();
            hotkeyManager.RegionPressed += () => TakeRegion();
            hotkeyManager.Register(settings);

            trayIcon = new NotifyIcon()
            {
                Icon = SystemIcons.Application,
                Text = "ScreenSnap",
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
            menu.Items.Add("⚙️ Настройки", null, OnSettings);
            menu.Items.Add("❌ Выход", null, OnExit);
            return menu;
        }

        private void TakeFullScreen()
        {
            var file = ScreenCapture.CaptureFullScreen();
            MessageBox.Show($"Сохранено:\n{file}", "ScreenSnap");
        }

        private void TakeRegion()
        {
            trayIcon.Visible = false;
            Thread.Sleep(200);
            var file = RegionSelector.CaptureRegion();
            trayIcon.Visible = true;
            if (file != null)
                MessageBox.Show($"Сохранено:\n{file}", "ScreenSnap");
            else
                MessageBox.Show("Отменено.", "ScreenSnap");
        }

        private void OnFullScreenshot(object? sender, EventArgs e) => TakeFullScreen();
        private void OnRegionScreenshot(object? sender, EventArgs e) => TakeRegion();

        private void OnSettings(object? sender, EventArgs e)
        {
            using var form = new SettingsForm(settings);
            if (form.ShowDialog() == DialogResult.OK)
                hotkeyManager.Register(settings); // переприменяем хоткеи после сохранения
        }

        private void OnExit(object? sender, EventArgs e)
        {
            hotkeyManager.Dispose();
            trayIcon.Visible = false;
            Application.Exit();
        }
    }
}