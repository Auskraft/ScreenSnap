using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenSnap
{
    public sealed class OnboardingForm : Form
    {
        private sealed record Step(string Icon, string Title, string Subtitle, string[] Bullets, string Hotkey);

        private static readonly Step[] Steps =
        {
            new("⚡", "Lightning-fast",
                "Скриншот за одно нажатие — без лишних движений.",
                new[]
                {
                    "PrintScreen — весь экран мгновенно",
                    "Ctrl+Shift+A — выделить область мышью",
                    "Ctrl+Shift+W — активное окно (скоро)",
                },
                "PrintScreen"),

            new("🔀", "Workflows",
                "Автоматизируй рутину — четыре пресета готовы к работе.",
                new[]
                {
                    "Quick Share — скриншот + Яндекс.Диск + ссылка",
                    "Documentation Mode — нумерованные шаги",
                    "Privacy Mode — автоблюр личных данных",
                    "Save & Pin — прикрепить в историю",
                },
                "Ctrl+Shift+S"),

            new("⌘", "Command Palette",
                "Всё управление — в одной строке поиска.",
                new[]
                {
                    "Ctrl+K — открыть в любой момент",
                    "Поиск команд, воркфлоу, истории",
                    "Навигация ↑↓ Enter Esc",
                },
                "Ctrl+K"),

            new("☁", "Cloud-native",
                "Яндекс.Диск встроен — ссылка готова за секунду.",
                new[]
                {
                    "Автозагрузка после каждого снимка",
                    "Публичная ссылка копируется в буфер",
                    "QR-код для быстрой передачи на телефон",
                },
                "Ctrl+Shift+S"),
        };

        private int   _step      = 0;
        private float _animAlpha = 1f;
        private float _dotAnim   = 0f;
        private readonly System.Windows.Forms.Timer _fadeTimer;
        private readonly System.Windows.Forms.Timer _dotTimer;

        private const int W       = 640;
        private const int H       = 480;
        private const int PadX    = 52;
        private const int PadTop  = 52;
        private const int BtnH    = 40;
        private const int BtnW    = 140;
        private const int Radius  = 20;

        // Используем обычные Button вместо IconButton — меньше конфликтов
        private readonly Button _btnNext;
        private readonly Button _btnSkip;

        private OnboardingForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            Size            = new Size(W, H);
            StartPosition   = FormStartPosition.CenterScreen;
            BackColor       = Color.FromArgb(18, 18, 30);
            DoubleBuffered  = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);

            ApplyRoundedRegion();

            // Кнопка «Далее»
            _btnNext = MakeButton("Далее →", BtnW, BtnH, accent: true);
            _btnNext.Click += OnNextClick;

            // Кнопка «Пропустить»
            _btnSkip = MakeButton("Пропустить", 120, BtnH, accent: false);
            _btnSkip.Click += (_, _) => Close();

            Controls.Add(_btnNext);
            Controls.Add(_btnSkip);

            LayoutButtons();

            // Fade анимация
            _fadeTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _fadeTimer.Tick += (_, _) =>
            {
                _animAlpha = Math.Min(1f, _animAlpha + 0.08f);
                Invalidate();
                if (_animAlpha >= 1f) _fadeTimer.Stop();
            };

            // Пульс точек — только раз в 100мс чтобы не мерцало
            _dotTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _dotTimer.Tick += (_, _) =>
            {
                _dotAnim = (_dotAnim + 0.3f) % (MathF.PI * 2);
                // Перерисовываем только нижнюю часть с точками
                Invalidate(new Rectangle(0, H - 70, W, 50));
            };
            _dotTimer.Start();
        }

        public static void ShowIfNeeded(AppSettings settings, bool force = false)
        {
            if (!force && settings.OnboardingShown) return;
            using var form = new OnboardingForm();
            form.ShowDialog();
            settings.OnboardingShown = true;
            settings.Save();
        }

        private void OnNextClick(object? sender, EventArgs e)
        {
            if (_step < Steps.Length - 1)
            {
                _step++;
                _btnNext.Text = _step == Steps.Length - 1 ? "Начать ✓" : "Далее →";
                _btnSkip.Visible = _step < Steps.Length - 1;
                _animAlpha = 0f;
                _fadeTimer.Start();
            }
            else
            {
                Close();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var t    = ThemeManager.Current;
            var step = Steps[_step];
            int alpha = (int)(_animAlpha * 255);

            // Фон
            g.Clear(Color.FromArgb(18, 18, 30));

            // Акцентный градиент сверху
            using (var topGrad = new LinearGradientBrush(
                new RectangleF(0, 0, W, 200),
                Color.FromArgb(25, t.Accent.A1.R, t.Accent.A1.G, t.Accent.A1.B),
                Color.Transparent,
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(topGrad, 0, 0, W, 200);
            }

            // Прогресс-полоска
            float progW = (float)(_step + 1) / Steps.Length * W;
            using (var progBrush = new LinearGradientBrush(
                new RectangleF(0, 0, Math.Max(progW, 1), 3),
                t.Accent.A1, t.Accent.A3, LinearGradientMode.Horizontal))
            {
                g.FillRectangle(progBrush, 0, 0, progW, 3);
            }

            // Иконка
            using (var iconFont = t.FontDisplay(48f))
            {
                TextRenderer.DrawText(g, step.Icon, iconFont,
                    new Rectangle(PadX, PadTop, 64, 64),
                    Color.FromArgb(alpha, t.Accent.A2));
            }

            // Номер шага
            using (var numFont = t.FontMono(10f))
            {
                TextRenderer.DrawText(g, $"{_step + 1} / {Steps.Length}", numFont,
                    new Rectangle(PadX, PadTop + 64, 64, 14),
                    Color.FromArgb(alpha, t.Text4),
                    TextFormatFlags.HorizontalCenter);
            }

            // Заголовок
            using (var headFont = t.FontDisplay(FontLoader.DisplayM, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, step.Title, headFont,
                    new Rectangle(PadX + 80, PadTop + 4, W - PadX * 2 - 80, 44),
                    Color.FromArgb(alpha, t.Text1));
            }

            // Subtitle
            using (var subFont = t.FontBody(FontLoader.BodyS))
            {
                TextRenderer.DrawText(g, step.Subtitle, subFont,
                    new Rectangle(PadX + 80, PadTop + 50, W - PadX * 2 - 80, 24),
                    Color.FromArgb(alpha, t.Text3));
            }

            // Разделитель
            int divY = PadTop + 100;
            using (var divPen = new Pen(Color.FromArgb(35, 255, 255, 255), 1f))
            {
                g.DrawLine(divPen, PadX, divY, W - PadX, divY);
            }

            // Буллеты
            int by = divY + 22;
            using (var bulletFont = t.FontBody(FontLoader.BodyM))
            {
                foreach (var bullet in step.Bullets)
                {
                    using var dotBrush = new SolidBrush(Color.FromArgb(alpha, t.Accent.A2));
                    g.FillEllipse(dotBrush, PadX, by + 8, 6, 6);

                    TextRenderer.DrawText(g, bullet, bulletFont,
                        new Rectangle(PadX + 18, by, W - PadX * 2 - 18, 24),
                        Color.FromArgb(alpha, t.Text2));
                    by += 34;
                }
            }

            // Хоткей-badge — правый нижний угол, над кнопкой
            var kbdRect = new Rectangle(W - PadX - 150, H - BtnH - 60, 150, 28);
            using (var kbdFill = new SolidBrush(Color.FromArgb(25, t.Accent.A1.R, t.Accent.A1.G, t.Accent.A1.B)))
            using (var kbdPen  = new Pen(Color.FromArgb(60, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B), 1f))
            using (var kbdPath = DrawingHelpers.RoundedRect(kbdRect, AppTheme.RButton))
            {
                g.FillPath(kbdFill, kbdPath);
                g.DrawPath(kbdPen, kbdPath);
            }
            using (var kbdFont = t.FontMono(11f))
            {
                TextRenderer.DrawText(g, step.Hotkey, kbdFont, kbdRect,
                    Color.FromArgb(alpha, t.Accent.A2),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // Точки-индикаторы
            DrawDots(g, t);
        }

        private void DrawDots(Graphics g, AppTheme t)
        {
            int total  = Steps.Length;
            int dotD   = 8;
            int gap    = 8;
            int totalW = total * dotD + (total - 1) * gap;
            int startX = (W - totalW) / 2;
            int dotY   = H - 52;

            for (int i = 0; i < total; i++)
            {
                int  x   = startX + i * (dotD + gap);
                bool act = i == _step;
                float pulse = act ? 1f + 0.2f * MathF.Sin(_dotAnim) : 1f;
                int  r   = (int)(dotD / 2 * pulse);
                int  cx  = x + dotD / 2;
                int  cy  = dotY + dotD / 2;

                using var b = new SolidBrush(act
                    ? t.Accent.A2
                    : Color.FromArgb(55, t.Text3));
                g.FillEllipse(b, cx - r, cy - r, r * 2, r * 2);
            }
        }

        private void LayoutButtons()
        {
            int btnY = H - BtnH - 20;
            _btnNext.Location = new Point(W - PadX - BtnW, btnY);
            _btnSkip.Location = new Point(PadX, btnY);
            _btnSkip.Visible  = true;
        }

        private static Button MakeButton(string text, int width, int height, bool accent)
        {
            var t = ThemeManager.Current;
            var btn = new Button
            {
                Text      = text,
                Size      = new Size(width, height),
                FlatStyle = FlatStyle.Flat,
                BackColor = accent
                    ? Color.FromArgb(80, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B)
                    : Color.FromArgb(30, 255, 255, 255),
                ForeColor = accent ? t.Accent.A2 : t.Text2,
                Font      = t.FontBody(FontLoader.BodyS),
                Cursor    = Cursors.Hand,
                TabStop   = false,
            };
            btn.FlatAppearance.BorderSize         = accent ? 1 : 0;
            if (accent)
                btn.FlatAppearance.BorderColor = Color.FromArgb(100, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B);
            btn.FlatAppearance.MouseOverBackColor = accent
                ? Color.FromArgb(110, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B)
                : Color.FromArgb(50, 255, 255, 255);
            btn.FlatAppearance.MouseDownBackColor = btn.FlatAppearance.MouseOverBackColor;
            return btn;
        }

        private void ApplyRoundedRegion()
        {
            using var path = DrawingHelpers.RoundedRect(new RectangleF(0, 0, W, H), Radius);
            Region = new Region(path);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fadeTimer.Dispose();
                _dotTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}