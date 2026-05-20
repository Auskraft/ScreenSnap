using System.Drawing;
using System.Windows.Forms;

namespace ScreenSnap
{
    public class TokenInputForm : Form
    {
        private TextBox txtToken = new();
        public string Token => txtToken.Text.Trim();

        public TokenInputForm()
        {
            Text = "ScreenSnap — Авторизация Яндекс.Диск";
            Size = new Size(500, 180);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 30, 30);
            ForeColor = Color.White;

            var lbl = new Label
            {
                Text = "Браузер открылся — авторизуйтесь и скопируйте токен из адресной строки:",
                Left = 20, Top = 20, Width = 450, AutoSize = false, Height = 35,
                ForeColor = Color.White
            };

            txtToken = new TextBox
            {
                Left = 20, Top = 60, Width = 450,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "Вставьте токен сюда..."
            };

            var btnOk = new Button
            {
                Text = "Подключить",
                Left = 20, Top = 95, Width = 120, Height = 30,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(txtToken.Text))
                    DialogResult = DialogResult.OK;
            };

            Controls.AddRange(new Control[] { lbl, txtToken, btnOk });
        }
    }
}