using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenSnap
{
    // ═══════════════════════════════════════════════════════════════════════
    //  ToastManager — стеклянный тост снизу-справа с прогрессом pipeline
    //
    //  Использование:
    //    var toast = ToastManager.Show("Quick Share", steps);
    //    toast.SetStep(1, ToastStepState.Done, 12);
    //    toast.SetStep(2, ToastStepState.Active);
    //    toast.Dismiss();
    //
    //  Stackable: каждый Show() создаёт новый тост, предыдущие сдвигаются вверх.
    // ═══════════════════════════════════════════════════════════════════════
    public enum ToastStepState { Pending, Active, Done, Error }

    public sealed class ToastWindow : Form
    {
        // ── Layout ───────────────────────────────────────────────────────────
        private const int ToastW    = 280;
        private const int PadX      = 16;
        private const int PadY      = 12;
        private const int StepH     = 24;
        private const int HeaderH   = 28;
        private const int Radius    = 12;
        private const int AutoDismissMs = 3000;

        private readonly string           _title;
        private readonly string[]         _stepLabels;
        private readonly ToastStepState[] _stepStates;
        private readonly long[]           _stepMs;
        private readonly System.Windows.Forms.Timer _slideTimer;
        private readonly System.Windows.Forms.Timer _dismissTimer;
        private float                     _slideOffset = 40f; // начинаем снизу

        // ── Анимация ─────────────────────────────────────────────────────────
        private bool _dismissing = false;
        private float _opacity   = 0f;

        public ToastWindow(string title, string[] steps, Point startPos)
        {
            _title      = title;
            _stepLabels = steps;
            _stepStates = new ToastStepState[steps.Length];
            _stepMs     = new long[steps.Length];

            int h = PadY + HeaderH + steps.Length * StepH + PadY;

            // Form
            FormBorderStyle = FormBorderStyle.None;
            Size            = new Size(ToastW, h);
            Location        = new Point(startPos.X, startPos.Y);
            BackColor       = Color.Black;
            TopMost         = true;
            ShowInTaskbar   = false;
            StartPosition   = FormStartPosition.Manual;
            Opacity         = 0;
            DoubleBuffered  = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);

            ApplyRoundedRegion();

            // Slide-in анимация
            _slideTimer   = new System.Windows.Forms.Timer { Interval = 16 };
            _slideTimer.Tick += OnSlideTick;

            _dismissTimer = new System.Windows.Forms.Timer { Interval = AutoDismissMs };
            _dismissTimer.Tick += (_, _) => { _dismissTimer.Stop(); StartDismiss(); };

            ThemeManager.ThemeChanged += (_, _) => Invalidate();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Обновить состояние шага (0-based). ms — время выполнения в мс.</summary>
        public void SetStep(int index, ToastStepState state, long ms = 0)
        {
            if (index < 0 || index >= _stepStates.Length) return;
            _stepStates[index] = state;
            _stepMs[index]     = ms;

            // Если все шаги Done или Error — запускаем таймер авто-скрытия
            bool allFinished = true;
            foreach (var s in _stepStates)
                if (s == ToastStepState.Pending || s == ToastStepState.Active)
                { allFinished = false; break; }

            if (allFinished && !_dismissTimer.Enabled)
                _dismissTimer.Start();

            if (!IsDisposed && IsHandleCreated)
                Invoke((Action)Invalidate);
        }

        public void Dismiss() => StartDismiss();

        // ── Появление ────────────────────────────────────────────────────────
        public new void Show()
        {
            base.Show();
            _slideTimer.Start();
        }

        private void OnSlideTick(object? sender, EventArgs e)
        {
            if (_dismissing)
            {
                _slideOffset = Math.Min(_slideOffset + 3f, 40f);
                _opacity     = Math.Max(_opacity - 0.06f, 0f);
            }
            else
            {
                _slideOffset = Math.Max(_slideOffset - 3f, 0f);
                _opacity     = Math.Min(_opacity + 0.06f, 1f);
            }

            Opacity = Math.Clamp(_opacity, 0, 1);
            Location = new Point(Location.X,
                Location.Y + (_dismissing ? (int)(_slideOffset * 0.3f) : -(int)(_slideOffset * 0.3f)));

            if (!_dismissing && _slideOffset <= 0 && _opacity >= 1f)
            {
                _slideTimer.Stop();
                Opacity = 1f;
            }
            else if (_dismissing && _opacity <= 0)
            {
                _slideTimer.Stop();
                Close();
            }
        }

        private void StartDismiss()
        {
            if (_dismissing) return;
            _dismissing = true;
            _slideTimer.Start();
        }

        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var t = ThemeManager.Current;

            // Фон — стеклянная карточка
            DrawingHelpers.DrawGlassCard(g,
                new RectangleF(0, 0, Width, Height), Radius,
                t.BgGlassStrong, t.Stroke2);

            int y = PadY;

            // Заголовок
            using var titleFont = t.FontBody(FontLoader.BodyS, System.Drawing.FontStyle.Bold);
            TextRenderer.DrawText(g, _title, titleFont,
                new Rectangle(PadX, y, Width - PadX * 2, HeaderH - 4), t.Text1);

            // Акцентная точка
            using var dotBrush = new SolidBrush(t.Accent.A2);
            g.FillEllipse(dotBrush, Width - PadX - 8, y + 8, 6, 6);

            y += HeaderH;

            // Разделитель
            using var divPen = new Pen(t.Stroke1, 0.5f);
            g.DrawLine(divPen, PadX, y, Width - PadX, y);
            y += 6;

            // Шаги
            for (int i = 0; i < _stepLabels.Length; i++)
            {
                DrawStep(g, t, i, PadX, y);
                y += StepH;
            }
        }

        private void DrawStep(Graphics g, AppTheme t, int i, int x, int y)
        {
            var state  = _stepStates[i];
            var label  = _stepLabels[i];
            var ms     = _stepMs[i];

            // Иконка
            string icon;
            Color  iconColor;

            switch (state)
            {
                case ToastStepState.Done:
                    icon      = "✓";
                    iconColor = t.Accent.A2;
                    break;
                case ToastStepState.Active:
                    icon      = "⟳";
                    iconColor = t.Text2;
                    break;
                case ToastStepState.Error:
                    icon      = "✕";
                    iconColor = Color.FromArgb(220, 80, 60);
                    break;
                default:
                    icon      = " ";
                    iconColor = t.Text4;
                    break;
            }

            using var iconFont = t.FontMono(11f);
            using var iconBrush = new SolidBrush(iconColor);
            TextRenderer.DrawText(g, icon, iconFont,
                new Rectangle(x, y + 2, 16, 18), iconColor);

            // Метка
            Color labelColor = state switch
            {
                ToastStepState.Done    => t.Text2,
                ToastStepState.Active  => t.Text1,
                ToastStepState.Error   => Color.FromArgb(220, 80, 60),
                _                      => t.Text4,
            };

            using var labelFont = t.FontBody(FontLoader.BodyXS,
                state == ToastStepState.Active
                    ? System.Drawing.FontStyle.Bold
                    : System.Drawing.FontStyle.Regular);

            TextRenderer.DrawText(g, label, labelFont,
                new Rectangle(x + 20, y + 3, Width - x * 2 - 60, 18), labelColor);

            // Время (если Done)
            if (state == ToastStepState.Done && ms > 0)
            {
                using var msFont = t.FontMono(10f);
                var msText = $"{ms}ms";
                TextRenderer.DrawText(g, msText, msFont,
                    new Rectangle(Width - PadX - 44, y + 3, 44, 18),
                    t.Text4, TextFormatFlags.Right);
            }

            // Активный: анимированная полоска прогресса
            if (state == ToastStepState.Active)
            {
                var barRect = new RectangleF(x + 20, y + StepH - 4, Width - x * 2 - 24, 2);
                using var bgBrush = new SolidBrush(t.Stroke1);
                using var bgPath  = DrawingHelpers.RoundedRect(barRect, 1f);
                g.FillPath(bgBrush, bgPath);

                // Бегущий огонёк
                int tick = (int)(DateTime.Now.Ticks / 200000) % 100;
                float pct  = tick / 100f;
                float gw   = barRect.Width * 0.35f;
                float gx   = barRect.X + pct * (barRect.Width + gw) - gw;
                var   gRect = new RectangleF(
                    Math.Max(barRect.X, gx),
                    barRect.Y,
                    Math.Min(gw, barRect.Right - Math.Max(barRect.X, gx)),
                    2f);
                if (gRect.Width > 0)
                {
                    using var glowBrush = new SolidBrush(
                        Color.FromArgb(180, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B));
                    using var glowPath = DrawingHelpers.RoundedRect(gRect, 1f);
                    g.FillPath(glowBrush, glowPath);
                }

                // Перерисовываем для анимации
                BeginInvoke((Action)(() => Invalidate(new Rectangle(x, y + StepH - 6, Width, 8))));
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void ApplyRoundedRegion()
        {
            using var path = DrawingHelpers.RoundedRect(
                new RectangleF(0, 0, Width, Height), Radius);
            Region = new Region(path);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _slideTimer.Dispose();
                _dismissTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ToastManager — статический менеджер стека тостов
    // ═══════════════════════════════════════════════════════════════════════
    public static class ToastManager
    {
        private static readonly List<ToastWindow> _stack = new();
        private const int MarginRight  = 24;
        private const int MarginBottom = 24;
        private const int StackGap     = 12;

        /// <summary>
        /// Создать и показать новый тост.
        /// steps — список меток шагов, например: ["Capture region", "Compress to WEBP", "Upload to Yandex", "Copy link"]
        /// </summary>
        public static ToastWindow Show(string title, string[] steps)
        {
            // Чистим закрытые
            _stack.RemoveAll(t => t.IsDisposed);

            var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);

            int h = 12 + 28 + steps.Length * 24 + 12;
            int x = screen.Right  - 280 - MarginRight;
            int y = screen.Bottom - h   - MarginBottom;

            // Сдвигаем существующие вверх
            foreach (var existing in _stack)
            {
                var loc = existing.Location;
                existing.Location = new Point(loc.X, loc.Y - h - StackGap);
            }

            var toast = new ToastWindow(title, steps, new Point(x, y + 40)); // +40 для slide-in
            _stack.Add(toast);

            toast.FormClosed += (_, _) =>
            {
                _stack.Remove(toast);
                toast.Dispose();
            };

            toast.Show();
            return toast;
        }

        /// <summary>Закрыть все тосты немедленно.</summary>
        public static void DismissAll()
        {
            foreach (var t in _stack.ToArray())
                t.Dismiss();
        }
    }
}