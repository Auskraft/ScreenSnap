using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenSnap
{
    // ═══════════════════════════════════════════════════════════════════════
    //  Страница (экран), которую отображает MainWindow
    // ═══════════════════════════════════════════════════════════════════════
    public enum AppScreen { Workflows, History, Editor, Settings }

    // ═══════════════════════════════════════════════════════════════════════
    //  SidebarControl — боковая панель 220px
    // ═══════════════════════════════════════════════════════════════════════
    public sealed class SidebarControl : UserControl
    {
        public event EventHandler<AppScreen>? ScreenRequested;

        private AppScreen _activeScreen = AppScreen.Workflows;

        private readonly Dictionary<AppScreen, int> _badges = new()
        {
            { AppScreen.Workflows, 4  },
            { AppScreen.History,   23 },
            { AppScreen.Editor,    0  },
            { AppScreen.Settings,  0  },
        };

        private int _cntPinned  = 3;
        private int _cntShared  = 19;
        private int _cntLocal   = 4;
        private int _cntTrash   = 12;

        private bool   _diskConnected = false;
        private bool   _diskSyncing   = false;
        private string _diskLogin     = "Не подключён";

        // ── Layout ────────────────────────────────────────────────────────────
        private const int PadX     = 14;
        private const int NavItemH = 36;
        private const int ColItemH = 28;
        private const int SecGap   = 20;
        private const int UserPodH = 52;

        // Отступы внутри nav-item
        private const int IconX     = PadX + 8;   // x иконки
        private const int IconW     = 22;
        private const int LabelX    = PadX + 34;  // x лейбла
        private const int BadgeW    = 28;          // ширина бейджа

        private readonly ToolTip _tooltip = new();

        public SidebarControl()
        {
            Width          = AppTheme.SidebarWidth;
            Dock           = DockStyle.Left;
            BackColor      = Color.Transparent;
            DoubleBuffered = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint  |
                     ControlStyles.OptimizedDoubleBuffer  |
                     ControlStyles.UserPaint              |
                     ControlStyles.ResizeRedraw, true);

            MouseClick += OnMouseClick;
            MouseMove  += OnMouseMove;

            ThemeManager.ThemeChanged += (_, _) => Invalidate();
        }

        // ── Public API ────────────────────────────────────────────────────────
        public void SetActive(AppScreen screen)       { _activeScreen = screen; Invalidate(); }
        public void SetBadge(AppScreen screen, int n) { _badges[screen] = n;    Invalidate(); }
        public int  GetBadge(AppScreen screen)        => _badges.GetValueOrDefault(screen, 0);

        public void SetDiskStatus(bool connected, bool syncing, string login = "")
        {
            _diskConnected = connected;
            _diskSyncing   = syncing;
            _diskLogin     = connected ? login : "Не подключён";
            Invalidate();
        }

        public void SetCollectionCounts(int pinned, int shared, int local, int trash)
        {
            _cntPinned = pinned; _cntShared = shared;
            _cntLocal  = local;  _cntTrash  = trash;
            Invalidate();
        }

        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var t = ThemeManager.Current;

            using var borderPen = new Pen(t.Stroke1, 1f);
            g.DrawLine(borderPen, Width - 1, 0, Width - 1, Height);

            int y = 16;

            DrawNavItem(g, t, ref y, AppScreen.Workflows, "⚡", "Workflows");
            DrawNavItem(g, t, ref y, AppScreen.History,   "🕐", "History");
            DrawNavItem(g, t, ref y, AppScreen.Editor,    "✏️", "Editor");
            DrawNavItem(g, t, ref y, AppScreen.Settings,  "⚙",  "Settings");

            y += SecGap;
            DrawDivider(g, t, y); y += 1 + SecGap;

            DrawSectionLabel(g, t, ref y, "Collections");
            DrawCollectionItem(g, t, ref y, "📌", "Pinned", _cntPinned);
            DrawCollectionItem(g, t, ref y, "🔗", "Shared", _cntShared);
            DrawCollectionItem(g, t, ref y, "💾", "Local",  _cntLocal);
            DrawCollectionItem(g, t, ref y, "🗑",  "Trash",  _cntTrash);

            y += SecGap;
            DrawDivider(g, t, y); y += 1 + SecGap;

            DrawSectionLabel(g, t, ref y, "Cloud");
            DrawDiskItem(g, t, ref y);

            DrawUserPod(g, t);
        }

        // ── Nav item ──────────────────────────────────────────────────────────
        private void DrawNavItem(Graphics g, AppTheme t, ref int y,
            AppScreen screen, string icon, string label)
        {
            bool active = _activeScreen == screen;
            var  rect   = new RectangleF(PadX, y, Width - PadX * 2, NavItemH);

            if (active)
            {
                using var path  = DrawingHelpers.RoundedRect(rect, AppTheme.RSm);
                using var brush = new SolidBrush(
                    Color.FromArgb(18, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B));
                g.FillPath(brush, path);

                using var accentBrush = new LinearGradientBrush(
                    new RectangleF(PadX, y, 3, NavItemH),
                    t.Accent.A1, t.Accent.A3, 90f);
                g.FillRectangle(accentBrush,
                    new RectangleF(PadX, y + 4, 3, NavItemH - 8));
            }
            else
            {
                var mp = PointToClient(Cursor.Position);
                if (rect.Contains(mp))
                {
                    using var path  = DrawingHelpers.RoundedRect(rect, AppTheme.RSm);
                    using var brush = new SolidBrush(Color.FromArgb(8, 255, 255, 255));
                    g.FillPath(brush, path);
                }
            }

            // Иконка — Segoe UI Emoji чтобы emoji рендерились корректно
            using var iconFont  = new Font("Segoe UI Emoji", 12f);
            var iconRect        = new Rectangle(IconX, y, IconW, NavItemH);
            TextRenderer.DrawText(g, icon, iconFont, iconRect,
                active ? t.Accent.A2 : t.Text3,
                TextFormatFlags.VerticalCenter);

            // Лейбл — ширина до бейджа
            int badge   = _badges.GetValueOrDefault(screen, 0);
            int badgeRW = badge > 0 ? BadgeW + 4 : 0;
            int labelW  = Width - LabelX - PadX - badgeRW;

            using var labelFont = active
                ? t.FontBody(FontLoader.BodyS, FontStyle.Bold)
                : t.FontBody(FontLoader.BodyS);
            var labelRect = new Rectangle(LabelX, y, labelW, NavItemH);
            TextRenderer.DrawText(g, label, labelFont, labelRect,
                active ? t.Text1 : t.Text2,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // Бейдж
            if (badge > 0)
            {
                var badgeText = badge > 99 ? "99+" : badge.ToString();
                using var bf  = t.FontMono(10f);
                var bs        = TextRenderer.MeasureText(badgeText, bf);
                int bw        = Math.Max(bs.Width + 8, 20);
                int bx        = Width - PadX - bw - 2;
                int by        = y + (NavItemH - 16) / 2;
                var badgeRect = new Rectangle(bx, by, bw, 16);

                using var bb = new SolidBrush(
                    Color.FromArgb(24, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B));
                using var bp = DrawingHelpers.RoundedRect(badgeRect, 8);
                g.FillPath(bb, bp);

                TextRenderer.DrawText(g, badgeText, bf, badgeRect,
                    active ? t.Accent.A2 : t.Text3,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            y += NavItemH + 2;
        }

        // ── Collection item ───────────────────────────────────────────────────
        private void DrawCollectionItem(Graphics g, AppTheme t, ref int y,
            string icon, string label, int count)
        {
            var rect = new RectangleF(PadX, y, Width - PadX * 2, ColItemH);

            var mp = PointToClient(Cursor.Position);
            if (rect.Contains(mp))
            {
                using var path  = DrawingHelpers.RoundedRect(rect, 6f);
                using var brush = new SolidBrush(Color.FromArgb(6, 255, 255, 255));
                g.FillPath(brush, path);
            }

            // Иконка
            using var iconFont = new Font("Segoe UI Emoji", 11f);
            var iconRect = new Rectangle(PadX + 6, y, 20, ColItemH);
            TextRenderer.DrawText(g, icon, iconFont, iconRect, t.Text3,
                TextFormatFlags.VerticalCenter);

            // Счётчик справа
            string cntText = count > 0 ? count.ToString() : "";
            int cntW = 0;
            if (count > 0)
            {
                using var cf = t.FontMono(10f);
                cntW = TextRenderer.MeasureText(cntText, cf).Width + 4;
                var cntRect = new Rectangle(Width - PadX - cntW, y, cntW, ColItemH);
                TextRenderer.DrawText(g, cntText, cf, cntRect, t.Text4,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }

            // Лейбл — между иконкой и счётчиком, с EndEllipsis
            int labelX = PadX + 30;
            int labelW = Width - labelX - PadX - cntW - 4;
            using var labelFont = t.FontBody(FontLoader.BodyXS);
            var labelRect = new Rectangle(labelX, y, labelW, ColItemH);
            TextRenderer.DrawText(g, label, labelFont, labelRect, t.Text2,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            y += ColItemH + 2;
        }

        // ── Yandex Disk item ──────────────────────────────────────────────────
        private void DrawDiskItem(Graphics g, AppTheme t, ref int y)
        {
            // Ширина карточки: от PadX до Width-PadX
            int cardW = Width - PadX * 2;
            var rect  = new RectangleF(PadX, y, cardW, 48);

            DrawingHelpers.DrawGlassCard(g, rect, AppTheme.RSm, t.BgGlass, t.Stroke1);

            // Я — логотип
            using var yFont = t.FontDisplay(14f, FontStyle.Bold);
            TextRenderer.DrawText(g, "Я", yFont,
                new Rectangle(PadX + 10, y + 8, 20, 20), t.Yandex);

            // Ширина правой части (если есть кнопка SYNC — отдаём ей 44px)
            bool showSync = _diskConnected;
            int  syncW    = showSync ? 44 : 0;
            int  textW    = cardW - 34 - syncW - 8; // 34 = отступ слева от Я

            // Название — с EndEllipsis на случай узкого сайдбара
            using var nameFont = t.FontBody(FontLoader.BodyXS, FontStyle.Bold);
            var nameRect = new Rectangle(PadX + 34, y + 8, textW, 16);
            TextRenderer.DrawText(g, "Яндекс.Диск", nameFont, nameRect, t.Text2,
                TextFormatFlags.EndEllipsis);

            // Статус
            using var stFont = t.FontBody(10f);
            Color  stColor;
            string stText;
            if (!_diskConnected)   { stText = "Отключён";         stColor = t.Text4;   }
            else if (_diskSyncing) { stText = "Синхронизация…";   stColor = t.Warning; }
            else                   { stText = "✓ Синхронизировано"; stColor = t.Success; }

            var stRect = new Rectangle(PadX + 34, y + 26, textW, 14);
            TextRenderer.DrawText(g, stText, stFont, stRect, stColor,
                TextFormatFlags.EndEllipsis);

            // Кнопка SYNC
            if (showSync)
            {
                var btnRect = new Rectangle(Width - PadX - syncW + 4, y + 14, 38, 20);
                using var btnPath = DrawingHelpers.RoundedRect(btnRect, 4f);
                using var btnB    = new SolidBrush(Color.FromArgb(20,
                    t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B));
                g.FillPath(btnB, btnPath);
                using var bp = new Pen(t.Accent.A2, 0.5f);
                g.DrawPath(bp, btnPath);

                using var sf = t.FontMono(9f);
                TextRenderer.DrawText(g, "SYNC", sf, btnRect, t.Accent.A2,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            y += 48 + 8;
        }

        // ── User pod ──────────────────────────────────────────────────────────
        private void DrawUserPod(Graphics g, AppTheme t)
        {
            int y = Height - UserPodH - 12;
            DrawDivider(g, t, y); y += 1 + 10;

            var avatarRect = new RectangleF(PadX + 4, y + (UserPodH - 32) / 2, 32, 32);
            DrawingHelpers.DrawAccentFill(g, avatarRect, t.Accent, 16f);

            using var initFont = t.FontDisplay(11f, FontStyle.Bold);
            TextRenderer.DrawText(g, "AU", initFont,
                Rectangle.Round(avatarRect), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            int textX = PadX + 42;
            int textW = Width - textX - PadX - 4;

            using var nameFont = t.FontBody(FontLoader.BodyXS, FontStyle.Bold);
            TextRenderer.DrawText(g, "Auskraft", nameFont,
                new Rectangle(textX, y + 6, textW, 16), t.Text1,
                TextFormatFlags.EndEllipsis);

            using var subFont = t.FontBody(10f);
            TextRenderer.DrawText(g, "Pro", subFont,
                new Rectangle(textX, y + 22, 40, 14), t.Accent.A2);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void DrawDivider(Graphics g, AppTheme t, int y)
        {
            using var pen = new Pen(t.Stroke1, 0.5f);
            g.DrawLine(pen, PadX + 4, y, Width - PadX - 4, y);
        }

        private void DrawSectionLabel(Graphics g, AppTheme t, ref int y, string text)
        {
            using var f = t.FontBody(10f, FontStyle.Bold);
            TextRenderer.DrawText(g, text.ToUpper(), f,
                new Rectangle(PadX + 8, y, Width - PadX * 2, 16), t.Text4);
            y += 20;
        }

        // ── Hit testing ───────────────────────────────────────────────────────
        private static readonly AppScreen[] NavOrder =
            { AppScreen.Workflows, AppScreen.History, AppScreen.Editor, AppScreen.Settings };

        private AppScreen? HitTestNav(Point p)
        {
            int y = 16;
            foreach (var screen in NavOrder)
            {
                var rect = new RectangleF(PadX, y, Width - PadX * 2, NavItemH);
                if (rect.Contains(p)) return screen;
                y += NavItemH + 2;
            }
            return null;
        }

        private void OnMouseClick(object? sender, MouseEventArgs e)
        {
            var hit = HitTestNav(e.Location);
            if (hit.HasValue && hit.Value != _activeScreen)
            {
                _activeScreen = hit.Value;
                Invalidate();
                ScreenRequested?.Invoke(this, hit.Value);
            }
        }

        private void OnMouseMove(object? sender, MouseEventArgs e) => Invalidate();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tooltip.Dispose();
            base.Dispose(disposing);
        }
    }
}