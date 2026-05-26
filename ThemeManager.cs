using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenSnap
{
    // ─────────────────────────────────────────────
    //  Accent palettes (from TOKENS.md §1)
    // ─────────────────────────────────────────────
    public enum AccentPalette { Violet, Cyan, Magenta, Lime }
    public enum AppThemeMode  { Dark, Light }

    public class AccentColors
    {
        public Color A1 { get; init; }   // gradient stop 1
        public Color A2 { get; init; }   // gradient stop 2 / main
        public Color A3 { get; init; }   // gradient stop 3
        public Color Glow { get; init; } // A2 at 45% alpha

        // Convenience: create a 135° linear brush for the accent gradient
        public LinearGradientBrush MakeGradientBrush(RectangleF rect)
            => new(rect, A1, A3, 135f);
    }

    // ─────────────────────────────────────────────
    //  Full token set for one mode+accent combo
    // ─────────────────────────────────────────────
    public class AppTheme
    {
        // Surfaces
        public Color Bg0 { get; init; }
        public Color Bg1 { get; init; }
        public Color Bg2 { get; init; }
        public Color Bg3 { get; init; }
        public Color BgGlass { get; init; }
        public Color BgGlassStrong { get; init; }
        public Color BgElevated { get; init; }

        // Strokes
        public Color Stroke1 { get; init; }
        public Color Stroke2 { get; init; }
        public Color Stroke3 { get; init; }

        // Text
        public Color Text1 { get; init; }
        public Color Text2 { get; init; }
        public Color Text3 { get; init; }
        public Color Text4 { get; init; }

        // Semantic
        public Color Success { get; init; } = Color.FromArgb(0x5A, 0xE3, 0xB5);
        public Color Danger  { get; init; } = Color.FromArgb(0xFF, 0x54, 0x70);
        public Color Warning { get; init; } = Color.FromArgb(0xFF, 0xB2, 0x3F);
        public Color Yandex  { get; init; } = Color.FromArgb(0xFC, 0x3F, 0x1D);

        // Accent (depends on palette)
        public AccentColors Accent { get; init; } = null!;

        // Radii (in pixels — use in GraphicsPath)
        public const int RXs     = 6;
        public const int RSm     = 10;
        public const int RMd     = 14;
        public const int RLg     = 20;
        public const int RXl     = 28;
        public const int RWindow = 16;
        public const int RButton = 8;
        public const int RInput  = 8;

        // Spacing
        public const int TitlebarHeight = 44;
        public const int SidebarWidth   = 220;

        // Fonts (resolved at runtime via FontLoader)
        public Font FontDisplay(float size, FontStyle style = FontStyle.Regular)
            => FontLoader.GetDisplay(size, style);
        public Font FontBody(float size, FontStyle style = FontStyle.Regular)
            => FontLoader.GetBody(size, style);
        public Font FontMono(float size, FontStyle style = FontStyle.Regular)
            => FontLoader.GetMono(size, style);
    }

    // ─────────────────────────────────────────────
    //  ThemeManager — singleton, raises ThemeChanged
    // ─────────────────────────────────────────────
    public static class ThemeManager
    {
        public static event EventHandler? ThemeChanged;

        private static AppThemeMode   _mode   = AppThemeMode.Dark;
        private static AccentPalette  _accent = AccentPalette.Violet;
        private static AppTheme?      _cache;

        public static AppThemeMode  Mode   => _mode;
        public static AccentPalette Accent => _accent;
        public static AppTheme      Current => _cache ??= Build(_mode, _accent);

        public static void SetMode(AppThemeMode mode)
        {
            if (_mode == mode) return;
            _mode  = mode;
            _cache = null;
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void SetAccent(AccentPalette accent)
        {
            if (_accent == accent) return;
            _accent = accent;
            _cache  = null;
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void Toggle() => SetMode(_mode == AppThemeMode.Dark
            ? AppThemeMode.Light : AppThemeMode.Dark);

        // ── Factory ─────────────────────────────
        private static AppTheme Build(AppThemeMode mode, AccentPalette palette)
        {
            var accent = BuildAccent(palette);

            return mode == AppThemeMode.Dark
                ? new AppTheme
                {
                    // dark surfaces
                    Bg0          = HexColor("#07070C"),
                    Bg1          = HexColor("#0A0A12"),
                    Bg2          = HexColor("#11111C"),
                    Bg3          = HexColor("#161624"),
                    BgGlass      = ArgbColor(20,  22,  35, (int)(0.55 * 255)),
                    BgGlassStrong= ArgbColor(14,  16,  26, (int)(0.78 * 255)),
                    BgElevated   = ArgbColor(28,  30,  46, (int)(0.85 * 255)),
                    // dark strokes
                    Stroke1      = ArgbColor(255, 255, 255, (int)(0.06 * 255)),
                    Stroke2      = ArgbColor(255, 255, 255, (int)(0.10 * 255)),
                    Stroke3      = ArgbColor(255, 255, 255, (int)(0.16 * 255)),
                    // dark text
                    Text1        = HexColor("#F5F7FF"),
                    Text2        = ArgbColor(245, 247, 255, (int)(0.66 * 255)),
                    Text3        = ArgbColor(245, 247, 255, (int)(0.42 * 255)),
                    Text4        = ArgbColor(245, 247, 255, (int)(0.24 * 255)),
                    Accent       = accent,
                }
                : new AppTheme
                {
                    // light surfaces
                    Bg0          = HexColor("#ECEEF5"),
                    Bg1          = HexColor("#F3F4FA"),
                    Bg2          = HexColor("#FFFFFF"),
                    Bg3          = HexColor("#F7F8FC"),
                    BgGlass      = ArgbColor(255, 255, 255, (int)(0.70 * 255)),
                    BgGlassStrong= ArgbColor(255, 255, 255, (int)(0.88 * 255)),
                    BgElevated   = ArgbColor(255, 255, 255, (int)(0.96 * 255)),
                    // light strokes
                    Stroke1      = ArgbColor(13,  16,  36, (int)(0.06 * 255)),
                    Stroke2      = ArgbColor(13,  16,  36, (int)(0.10 * 255)),
                    Stroke3      = ArgbColor(13,  16,  36, (int)(0.16 * 255)),
                    // light text
                    Text1        = HexColor("#0B0D1C"),
                    Text2        = ArgbColor(11,  13,  28, (int)(0.66 * 255)),
                    Text3        = ArgbColor(11,  13,  28, (int)(0.42 * 255)),
                    Text4        = ArgbColor(11,  13,  28, (int)(0.22 * 255)),
                    Accent       = accent,
                };
        }

        private static AccentColors BuildAccent(AccentPalette p) => p switch
        {
            AccentPalette.Violet  => new AccentColors
            {
                A1   = HexColor("#4D6BFF"),
                A2   = HexColor("#6E4BFF"),
                A3   = HexColor("#9B5CFF"),
                Glow = ArgbColor(110, 75, 255, (int)(0.45 * 255)),
            },
            AccentPalette.Cyan    => new AccentColors
            {
                A1   = HexColor("#00D4FF"),
                A2   = HexColor("#0098FF"),
                A3   = HexColor("#4D6BFF"),
                Glow = ArgbColor(0, 152, 255, (int)(0.45 * 255)),
            },
            AccentPalette.Magenta => new AccentColors
            {
                A1   = HexColor("#FF4DD2"),
                A2   = HexColor("#B14BFF"),
                A3   = HexColor("#6E4BFF"),
                Glow = ArgbColor(177, 75, 255, (int)(0.45 * 255)),
            },
            AccentPalette.Lime    => new AccentColors
            {
                A1   = HexColor("#A6FF4D"),
                A2   = HexColor("#5AE3B5"),
                A3   = HexColor("#00D4FF"),
                Glow = ArgbColor(90, 227, 181, (int)(0.45 * 255)),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(p))
        };

        // ── Helpers ─────────────────────────────
        private static Color HexColor(string hex)
            => ColorTranslator.FromHtml(hex);

        /// <summary>r, g, b in 0-255; a in 0-255</summary>
        private static Color ArgbColor(int r, int g, int b, int a)
            => Color.FromArgb(a, r, g, b);
    }

    // ─────────────────────────────────────────────
    //  GDI+ helpers used across the app
    // ─────────────────────────────────────────────
    public static class DrawingHelpers
    {
        /// <summary>Create a rounded-rectangle GraphicsPath.</summary>
        public static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            var d  = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.X,             rect.Y,             d, d, 180, 90);
            path.AddArc(rect.Right - d,     rect.Y,             d, d, 270, 90);
            path.AddArc(rect.Right - d,     rect.Bottom - d,    d, d, 0,   90);
            path.AddArc(rect.X,             rect.Bottom - d,    d, d, 90,  90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Paint a glass card: rounded rect with semi-transparent fill + hairline stroke.
        /// </summary>
        public static void DrawGlassCard(Graphics g, RectangleF rect, float radius,
            Color fill, Color stroke)
        {
            using var path = RoundedRect(rect, radius);
            using var brush = new SolidBrush(fill);
            g.FillPath(brush, path);
            using var pen = new Pen(stroke, 1f);
            g.DrawPath(pen, path);
        }

        /// <summary>
        /// Draw a 3-stop linear gradient fill (135°) clipped to a rounded rect.
        /// Used for accent buttons, logo square, etc.
        /// </summary>
        public static void DrawAccentFill(Graphics g, RectangleF rect, AccentColors accent,
            float radius = AppTheme.RButton)
        {
            using var path = RoundedRect(rect, radius);
            g.SetClip(path);
            // LinearGradientBrush only does 2 stops — simulate 3 with a blend
            using var brush = new LinearGradientBrush(rect, accent.A1, accent.A3, 135f);
            var blend = new ColorBlend(3)
            {
                Colors = new[] { accent.A1, accent.A2, accent.A3 },
                Positions = new[] { 0f, 0.5f, 1f }
            };
            brush.InterpolationColors = blend;
            g.FillRectangle(brush, rect);
            g.ResetClip();
        }
    }
}