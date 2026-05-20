using System.Drawing;
using System.Windows.Forms;

namespace ScreenSnap
{
    public class SettingsForm : Form
    {
        private AppSettings settings;
        private TextBox txtSavePath = new();
        private ComboBox cmbFormat = new();
        private TextBox txtHotkeyFull = new();
        private TextBox txtHotkeyRegion = new();
        private Label lblHotkeyFullPreview = new();
        private Label lblHotkeyRegionPreview = new();

        public SettingsForm(AppSettings settings)
        {
            this.settings = settings;
            BuildUI();
            LoadValues();
        }

        private void BuildUI()
        {
            Text = "ScreenSnap — Настройки";
            Size = new Size(440, 360);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;

            int y = 20;

            // Папка сохранения
            AddLabel("Папка сохранения:", 20, y);
            y += 25;
            txtSavePath = AddTextBox(20, y, 300);
            var btnBrowse = AddButton("...", 330, y, 60);
            btnBrowse.Click += (s, e) =>
            {
                using var dlg = new FolderBrowserDialog();
                dlg.SelectedPath = txtSavePath.Text;
                if (dlg.ShowDialog() == DialogResult.OK)
                    txtSavePath.Text = dlg.SelectedPath;
            };
            y += 40;

            // Формат
            AddLabel("Формат файла:", 20, y);
            y += 25;
            cmbFormat = new ComboBox
            {
                Left = 20, Top = y, Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White
            };
            cmbFormat.Items.AddRange(new[] { "PNG", "JPG" });
            Controls.Add(cmbFormat);
            y += 40;

            // Хоткей полный экран
            AddLabel("Хоткей — полный экран:", 20, y);
            y += 25;
            txtHotkeyFull = AddHotkeyBox(20, y);
            txtHotkeyFull.KeyDown += (s, e) => CaptureHotkey(e, txtHotkeyFull, lblHotkeyFullPreview);
            lblHotkeyFullPreview = AddLabel("", 220, y + 3);
            y += 40;

            // Хоткей область
            AddLabel("Хоткей — выделить область:", 20, y);
            y += 25;
            txtHotkeyRegion = AddHotkeyBox(20, y);
            txtHotkeyRegion.KeyDown += (s, e) => CaptureHotkey(e, txtHotkeyRegion, lblHotkeyRegionPreview);
            lblHotkeyRegionPreview = AddLabel("", 220, y + 3);
            y += 50;

            // Кнопки
            var btnSave = AddButton("Сохранить", 20, y, 120);
            btnSave.BackColor = Color.FromArgb(0, 120, 212);
            btnSave.Click += OnSave;

            var btnCancel = AddButton("Отмена", 160, y, 100);
            btnCancel.Click += (s, e) => Close();
        }

        private void CaptureHotkey(KeyEventArgs e, TextBox box, Label preview)
        {
            e.SuppressKeyPress = true;
            var parts = new List<string>();
            if (e.Control) parts.Add("Ctrl");
            if (e.Shift) parts.Add("Shift");
            if (e.Alt) parts.Add("Alt");

            var key = e.KeyCode;
            if (key != Keys.ControlKey && key != Keys.ShiftKey && key != Keys.Menu)
                parts.Add(key.ToString());

            if (parts.Count > 1)
            {
                box.Text = string.Join("+", parts);
                preview.Text = "✅";
                preview.ForeColor = Color.LightGreen;
            }
        }

        private void LoadValues()
        {
            txtSavePath.Text = settings.SavePath;
            cmbFormat.SelectedItem = settings.FileFormat;
            txtHotkeyFull.Text = settings.HotkeyFullScreen;
            txtHotkeyRegion.Text = settings.HotkeyRegion;
        }

        private void OnSave(object? sender, EventArgs e)
        {
            settings.SavePath = txtSavePath.Text;
            settings.FileFormat = cmbFormat.SelectedItem?.ToString() ?? "PNG";
            settings.HotkeyFullScreen = txtHotkeyFull.Text;
            settings.HotkeyRegion = txtHotkeyRegion.Text;
            settings.Save();

            ScreenCapture.SavePath = settings.SavePath;

            DialogResult = DialogResult.OK;
            Close();
        }

        // Хелперы
        private Label AddLabel(string text, int x, int y)
        {
            var lbl = new Label { Text = text, Left = x, Top = y, AutoSize = true, ForeColor = Color.White };
            Controls.Add(lbl);
            return lbl;
        }

        private TextBox AddTextBox(int x, int y, int width)
        {
            var tb = new TextBox
            {
                Left = x, Top = y, Width = width,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(tb);
            return tb;
        }

        private TextBox AddHotkeyBox(int x, int y)
        {
            var tb = new TextBox
            {
                Left = x, Top = y, Width = 190,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = true
            };
            Controls.Add(tb);
            return tb;
        }

        private Button AddButton(string text, int x, int y, int width)
        {
            var btn = new Button
            {
                Text = text, Left = x, Top = y, Width = width, Height = 30,
                BackColor = Color.FromArgb(60, 60, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            Controls.Add(btn);
            return btn;
        }
    }
}