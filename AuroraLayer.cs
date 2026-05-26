using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenSnap
{
    /// <summary>
    /// Full-size transparent Panel that paints the animated aurora background
    /// from the design spec (styles.css .aurora + @keyframes aurora).
    ///
    /// Place this as the LOWEST z-order child of MainWindow's client area
    /// (or directly on the Form) — everything else sits on top.
    ///
    /// Lifecycle:
    ///   var aurora = new AuroraLayer();
    ///   Controls.Add(aurora);
    ///   aurora.BringToFront(); // then send to back after adding other controls
    ///   aurora.SendToBack();
    ///
    /// The layer re-paints itself on a 30 fps timer and responds to
    /// ThemeManager.ThemeChanged so colours update when the user switches accent.
    /// </summary>
    public sealed class AuroraLayer : Panel
    {
        // ── Animation state ───────────────────────────────────────────────────
        // Total cycle: 24 000 ms (from --motion.duration.aurora)
        private const double CycleDurationMs = 24_000.0;
        private const int    FrameIntervalMs = 33;  // ≈ 30 fps

        private readonly System.Windows.Forms.Timer _timer;
        private double _elapsedMs;
        private DateTime _lastTick;

        // ── Gradient blobs ─────────────────────────────────────────────────
        // Each blob: (relative cx, cy, rx, ry) — all in 0..1 space
        // Matches the three radial-gradient origins in the CSS aurora class.
        private static readonly (float cx, float cy, float rx, float ry)[] Blobs =
        [
            (0.20f, 0.20f, 0.55f, 0.55f),  // accent-1 blob  (top-left)
            (0.80f, 0.30f, 0.60f, 0.60f),  // accent-3 blob  (top-right)
            (0.50f, 0.80f, 0.50f, 0.50f),  // accent-2 blob  (bottom-centre)
        ];

        // ── Constructor ───────────────────────────────────────────────────────
        public AuroraLayer()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint  |
                ControlStyles.OptimizedDoubleBuffer  |
                ControlStyles.UserPaint              |
                ControlStyles.ResizeRedraw,
                true);

            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;

            _lastTick = DateTime.UtcNow;
            _timer = new System.Windows.Forms.Timer { Interval = FrameIntervalMs, Enabled = false };
            _timer.Tick += OnTick;

            ThemeManager.ThemeChanged += (_, _) => Invalidate();
        }

        // ── Public control ────────────────────────────────────────────────────
        public void StartAnimation()
        {
            _lastTick = DateTime.UtcNow;
            _timer.Start();
        }

        public void StopAnimation() => _timer.Stop();

        // ── Timer tick ────────────────────────────────────────────────────────
        private void OnTick(object? sender, EventArgs e)
        {
            var now = DateTime.UtcNow;
            _elapsedMs += (now - _lastTick).TotalMilliseconds;
            _lastTick = now;

            if (_elapsedMs >= CycleDurationMs) _elapsedMs -= CycleDurationMs;
            Invalidate();
        }

        // ── Painting ─────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.CompositingMode   = CompositingMode.SourceOver;
            g.CompositingQuality= CompositingQuality.HighSpeed; // perf over quality

            var t  = ThemeManager.Current;
            var acc = t.Accent;
            var w  = (float)Width;
            var h  = (float)Height;

            // ── Background fill ────────────────────────────────────────────
            // Dark: #050509  /  Light: #DEE3F2
            Color bg = ThemeManager.Mode == AppThemeMode.Dark
                ? Color.FromArgb(255, 5,  5,  9)
                : Color.FromArgb(255, 222, 227, 242);

            g.Clear(bg);

            // ── Aurora animation transform ─────────────────────────────────
            // CSS keyframes: translate 0→(2%,-3%)→(-3%,2%), scale 1→1.05→0.98, opacity 0.32→0.40→0.28
            // Map _elapsedMs (0..CycleDurationMs) to t ∈ [0,1], then split into two sub-arcs.
            double norm = _elapsedMs / CycleDurationMs;   // 0..1, linear

            // Use a smooth triangle wave so we go 0→1→0 over the full cycle
            double phase = norm < 0.5
                ? EaseInOut(norm * 2.0)          // 0..1
                : EaseInOut((1.0 - norm) * 2.0); // 1..0

            float tx = (float)(w * Lerp(0.0, 0.02,  phase));
            float ty = (float)(h * Lerp(0.0, -0.03, phase));
            float sc = (float)Lerp(1.0, 1.05, phase);
            float op = (float)Lerp(0.32, 0.40, phase);

            // Apply transform centred on the panel
            var m = new Matrix();
            m.Translate(w / 2f + tx, h / 2f + ty);
            m.Scale(sc, sc);
            m.Translate(-w / 2f, -h / 2f);
            g.Transform = m;

            // ── Draw blobs ─────────────────────────────────────────────────
            Color[] blobColors = [ acc.A1, acc.A3, acc.A2 ];

            for (int i = 0; i < Blobs.Length; i++)
            {
                var (cx, cy, rx, ry) = Blobs[i];

                // Gentle individual blob drift
                double drift = phase + i * 0.25;
                float bx = w * (cx + (float)(Math.Sin(drift * Math.PI * 2) * 0.03));
                float by = h * (cy + (float)(Math.Cos(drift * Math.PI * 2) * 0.03));
                float bw = w * rx;
                float bh = h * ry;

                var rect = new RectangleF(bx - bw / 2f, by - bh / 2f, bw, bh);
                DrawBlob(g, rect, blobColors[i], op * 0.35f);
            }

            g.ResetTransform();
        }

        // ── Draw a single elliptical gradient blob ─────────────────────────
        private static void DrawBlob(Graphics g, RectangleF rect, Color color, float opacity)
        {
            // Clamp rect to avoid degenerate path
            if (rect.Width < 4 || rect.Height < 4) return;

            // We simulate radial-gradient(circle, color, transparent) with a PathGradientBrush
            using var path = new GraphicsPath();
            path.AddEllipse(rect);

            using var brush = new PathGradientBrush(path)
            {
                CenterColor    = Color.FromArgb((int)(opacity * 255), color),
                SurroundColors = new[] { Color.FromArgb(0, color) },
            };

            // Blur falloff — PathGradientBrush focuses tightly by default; flatten it
            brush.FocusScales = new PointF(0.0f, 0.0f);

            g.FillPath(brush, path);
        }

        // ── Math helpers ──────────────────────────────────────────────────────
        private static double EaseInOut(double t)
            => t * t * (3.0 - 2.0 * t);   // smoothstep

        private static double Lerp(double a, double b, double t)
            => a + (b - a) * t;

        // ── Cleanup ───────────────────────────────────────────────────────────
        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }
}