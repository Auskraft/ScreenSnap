using System.Windows.Forms;
using System.Drawing;

namespace ScreenSnap
{
    public class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;

        public TrayApplicationContext()
        {
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

        private void OnFullScreenshot(object? sender, EventArgs e)
        {
            var file = ScreenCapture.CaptureFullScreen();
            MessageBox.Show($"Сохранено:\n{file}", "ScreenSnap");
        }

        private void OnRegionScreenshot(object? sender, EventArgs e)
        {
            MessageBox.Show("Выделение области — скоро сделаем!");
        }

        private void OnSettings(object? sender, EventArgs e)
        {
            MessageBox.Show("Настройки — скоро сделаем!");
        }

        private void OnExit(object? sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }
    }
}