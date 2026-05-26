using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenSnap
{
    // ═══════════════════════════════════════════════════════════════════════
    //  WorkflowsScreen — стартовый экран Фазы 2
    //
    //  Содержит:
    //    • 4 пресета (Quick Share / Doc / Privacy / Save & Pin)
    //    • Flow-builder: блоки + стрелки
    //    • Библиотека 16 actions справа
    // ═══════════════════════════════════════════════════════════════════════
    public sealed class WorkflowsScreen : UserControl
    {
        // ── Пресеты ──────────────────────────────────────────────────────────
        private record Preset(string Icon, string Name, string Hotkey, string[] Steps, Color Tint);

        private readonly Preset[] _presets;

        private int   _activePreset   = 0;
        private int?  _hoveredPreset  = null;
        private int?  _hoveredAction  = null;

        // ── Actions library ───────────────────────────────────────────────────
        private record ActionDef(string Icon, string Id, bool IsNew = false);

        private static readonly ActionDef[] Actions =
        {
            new("📷", "trigger"),   new("📋", "copy"),    new("💾", "save"),
            new("✏️", "editor"),    new("☁", "upload"),   new("🔗", "link"),
            new("📂", "open"),      new("□", "qr"),       new("📌", "pin"),
            new("📁", "folder"),    new("🔔", "notify"),  new("🌫", "blur"),
            new("📦", "compress", true),  new("🔢", "steps", true),
            new("↗", "share",    true),   new("🔒", "private", true),
            // markdown — 17-й (показываем если влезет)
            new("MD", "markdown", true),
        };

        // Flow-builder: текущие шаги активного пресета (индексы в Actions)
        private List<int> _flowSteps = new();

        // ── Layout ───────────────────────────────────────────────────────────
        private const int PresetCardW  = 180;
        private const int PresetCardH  = 78;
        private const int PresetGap    = 10;
        private const int LibW         = 200;
        private const int ActionCellW  = 86;
        private const int ActionCellH  = 52;
        private const int ActionCols   = 2;

        // ── Scroll ────────────────────────────────────────────────────────────
        private readonly VScrollBar _scroll;

        // ─────────────────────────────────────────────────────────────────────
        public WorkflowsScreen()
        {
            Dock          = DockStyle.Fill;
            BackColor     = Color.Transparent;
            DoubleBuffered = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint  |
                     ControlStyles.OptimizedDoubleBuffer  |
                     ControlStyles.UserPaint              |
                     ControlStyles.ResizeRedraw, true);

            // Пресеты
            _presets = new[]
            {
                new Preset("⚡", "Quick Share",       "Ctrl+Shift+S",
                    new[] { "trigger", "compress", "upload", "share" },
                    Color.FromArgb(110, 75, 255)),
                new Preset("✏️", "Documentation Mode", "Ctrl+Shift+D",
                    new[] { "trigger", "editor", "steps", "save" },
                    Color.FromArgb(90, 200, 140)),
                new Preset("🛡", "Privacy Mode",       "Ctrl+Shift+P",
                    new[] { "trigger", "blur", "upload", "private", "link" },
                    Color.FromArgb(80, 160, 255)),
                new Preset("📌", "Save & Pin",         "Ctrl+Shift+T",
                    new[] { "trigger", "save", "pin" },
                    Color.FromArgb(255, 160, 60)),
            };

            LoadPresetSteps(_activePreset);

            _scroll = new VScrollBar
            {
                Dock    = DockStyle.Right,
                Width   = 0, // скрыт — прокрутка колесом
                Visible = false,
            };
            Controls.Add(_scroll);

            MouseClick += OnMouseClick;
            MouseMove  += OnMouseMove;
            MouseWheel += (_, e) => { _scroll.Value = Math.Clamp(
                _scroll.Value - e.Delta / 3, _scroll.Minimum, _scroll.Maximum);
                Invalidate(); };

            ThemeManager.ThemeChanged += (_, _) => Invalidate();
        }

        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var t   = ThemeManager.Current;
            int pad = 28;
            int y   = pad;

            // ── Заголовок ────────────────────────────────────────────────────
            using var titleFont = t.FontDisplay(FontLoader.DisplayM, FontStyle.Bold);
            TextRenderer.DrawText(g, "Workflows", titleFont,
                new Rectangle(pad, y, Width - pad * 2 - LibW, 36), t.Text1);
            y += 44;

            using var subFont = t.FontBody(FontLoader.BodyXS);
            TextRenderer.DrawText(g, "Автоматизируй скриншоты. Запусти пресет или собери свой flow.", subFont,
                new Rectangle(pad, y, Width - pad * 2 - LibW, 20), t.Text3);
            y += 32;

            // ── Пресет-карточки ───────────────────────────────────────────────
            DrawPresetRow(g, t, pad, y);
            y += PresetCardH + 24;

            // ── Разделитель + заголовок flow ──────────────────────────────────
            using var divPen = new Pen(t.Stroke1, 0.5f);
            g.DrawLine(divPen, pad, y, Width - LibW - pad, y);
            y += 12;

            using var flowLabelFont = t.FontBody(FontLoader.BodyXS, FontStyle.Bold);
            TextRenderer.DrawText(g, "FLOW BUILDER", flowLabelFont,
                new Rectangle(pad, y, 200, 16), t.Text3);

            var presetName = _presets[_activePreset].Name;
            using var pNameFont = t.FontBody(FontLoader.BodyXS);
            TextRenderer.DrawText(g, $"— {presetName}", pNameFont,
                new Rectangle(pad + 120, y, 200, 16), t.Text4);
            y += 28;

            // ── Flow builder ──────────────────────────────────────────────────
            DrawFlowBuilder(g, t, pad, y, Width - LibW - pad * 2);
            y += 80;

            // ── Правая панель: Actions library ────────────────────────────────
            DrawActionsLibrary(g, t);
        }

        // ── Пресет-карточки ───────────────────────────────────────────────────
        private void DrawPresetRow(Graphics g, AppTheme t, int startX, int y)
        {
            int x = startX;
            for (int i = 0; i < _presets.Length; i++)
            {
                DrawPresetCard(g, t, _presets[i], i, x, y);
                x += PresetCardW + PresetGap;
            }
        }

        private void DrawPresetCard(Graphics g, AppTheme t, Preset p, int idx, int x, int y)
        {
            bool active  = _activePreset  == idx;
            bool hovered = _hoveredPreset == idx;

            var rect = new RectangleF(x, y, PresetCardW, PresetCardH);

            // Фон карточки
            Color fill;
            if (active)       fill = Color.FromArgb(22, p.Tint.R, p.Tint.G, p.Tint.B);
            else if (hovered)  fill = Color.FromArgb(10, 255, 255, 255);
            else               fill = t.BgGlass;

            Color stroke = active
                ? Color.FromArgb(80, p.Tint.R, p.Tint.G, p.Tint.B)
                : t.Stroke1;

            DrawingHelpers.DrawGlassCard(g, rect, AppTheme.RSm, fill, stroke);

            // Акцентная полоска сверху (только active)
            if (active)
            {
                using var stripBrush = new LinearGradientBrush(
                    new RectangleF(x, y, PresetCardW, 3),
                    p.Tint, Color.FromArgb(p.Tint.A, p.Tint.R, p.Tint.G, p.Tint.B), 0f);
                g.FillRectangle(stripBrush, x + 14, y, PresetCardW - 28, 2);
            }

            // Иконка
            using var iconFont = t.FontBody(18f);
            TextRenderer.DrawText(g, p.Icon, iconFont,
                new Rectangle(x + 14, y + 10, 28, 28), t.Text2);

            // Название
            using var nameFont = t.FontBody(FontLoader.BodyS,
                active ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(g, p.Name, nameFont,
                new Rectangle(x + 14, y + 36, PresetCardW - 20, 16),
                active ? t.Text1 : t.Text2);

            // Хоткей
            using var hkFont = t.FontMono(10f);
            TextRenderer.DrawText(g, p.Hotkey, hkFont,
                new Rectangle(x + 14, y + 56, PresetCardW - 20, 14),
                active ? Color.FromArgb(180, p.Tint.R, p.Tint.G, p.Tint.B) : t.Text4);
        }

        // ── Flow builder ──────────────────────────────────────────────────────
        private void DrawFlowBuilder(Graphics g, AppTheme t, int x, int y, int maxWidth)
        {
            if (_flowSteps.Count == 0)
            {
                using var ef = t.FontBody(FontLoader.BodyXS);
                TextRenderer.DrawText(g, "Пресет не содержит шагов", ef,
                    new Rectangle(x, y, maxWidth, 60), t.Text4,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            int blockW  = 110;
            int blockH  = 48;
            int arrowW  = 24;
            int cx      = x;
            var acc     = _presets[_activePreset].Tint;

            foreach (int ai in _flowSteps)
            {
                var action = Actions[ai];

                // Блок
                var blockRect = new RectangleF(cx, y, blockW, blockH);
                DrawingHelpers.DrawGlassCard(g, blockRect, AppTheme.RSm, t.BgGlass, t.Stroke2);

                // Иконка + подпись
                using var iconF = t.FontBody(14f);
                TextRenderer.DrawText(g, action.Icon, iconF,
                    new Rectangle(cx + 10, y + 6, 20, 20), t.Accent.A2);

                using var idF = t.FontMono(10f);
                TextRenderer.DrawText(g, action.Id, idF,
                    new Rectangle(cx + 10, y + 28, blockW - 16, 14), t.Text3);

                // NEW-бейдж
                if (action.IsNew)
                {
                    var nr = new Rectangle(cx + blockW - 30, y + 4, 26, 13);
                    using var nb = new SolidBrush(Color.FromArgb(22, acc.R, acc.G, acc.B));
                    using var np = DrawingHelpers.RoundedRect(nr, 4f);
                    g.FillPath(nb, np);
                    using var npen = new Pen(Color.FromArgb(70, acc.R, acc.G, acc.B), 0.5f);
                    g.DrawPath(npen, np);
                    using var nf = t.FontMono(8f);
                    TextRenderer.DrawText(g, "NEW", nf, nr,
                        Color.FromArgb(200, acc.R, acc.G, acc.B),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }

                cx += blockW;

                // Стрелка (не после последнего)
                if (ai != _flowSteps[^1])
                {
                    int ay = y + blockH / 2;
                    using var arrowPen = new Pen(
                        Color.FromArgb(100, acc.R, acc.G, acc.B), 1.5f);
                    arrowPen.CustomEndCap = new AdjustableArrowCap(4, 4);
                    g.DrawLine(arrowPen, cx, ay, cx + arrowW, ay);
                    cx += arrowW;
                }

                // Не выходим за maxWidth
                if (cx + blockW > x + maxWidth) break;
            }
        }

        // ── Actions Library (правая панель) ────────────────────────────────────
        private void DrawActionsLibrary(Graphics g, AppTheme t)
        {
            int lx  = Width - LibW - 4;
            int ly  = 28;

            // Фоновая панель
            DrawingHelpers.DrawGlassCard(g,
                new RectangleF(lx - 8, 0, LibW + 12, Height),
                0f, t.BgGlass, t.Stroke1);

            using var hFont = t.FontBody(FontLoader.BodyXS, FontStyle.Bold);
            TextRenderer.DrawText(g, "ACTIONS", hFont,
                new Rectangle(lx, ly, LibW, 16), t.Text3);
            ly += 24;

            using var subF = t.FontBody(10f);
            TextRenderer.DrawText(g, "Перетащи в flow builder", subF,
                new Rectangle(lx, ly, LibW, 14), t.Text4);
            ly += 22;

            int maxRows = (Height - ly - 20) / (ActionCellH + 4);
            int maxShow = Math.Min(Actions.Length, maxRows * ActionCols);

            for (int i = 0; i < maxShow; i++)
            {
                int row = i / ActionCols;
                int col = i % ActionCols;
                int ax  = lx + col * (ActionCellW + 4);
                int ay  = ly + row * (ActionCellH + 4);

                DrawActionCell(g, t, Actions[i], i, ax, ay);
            }
        }

        private void DrawActionCell(Graphics g, AppTheme t, ActionDef a, int idx, int x, int y)
        {
            bool hovered = _hoveredAction == idx;
            var rect = new RectangleF(x, y, ActionCellW, ActionCellH);

            Color fill   = hovered
                ? Color.FromArgb(14, 255, 255, 255)
                : Color.FromArgb(6,  255, 255, 255);
            DrawingHelpers.DrawGlassCard(g, rect, AppTheme.RSm, fill, t.Stroke1);

            // NEW-метка
            if (a.IsNew)
            {
                var acc = t.Accent;
                var nr  = new Rectangle(x + ActionCellW - 26, y + 4, 22, 11);
                using var nb  = new SolidBrush(Color.FromArgb(20, acc.A2.R, acc.A2.G, acc.A2.B));
                using var np  = DrawingHelpers.RoundedRect(nr, 3f);
                g.FillPath(nb, np);
                using var nf  = t.FontMono(8f);
                TextRenderer.DrawText(g, "NEW", nf, nr, acc.A2,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            using var iconF = t.FontBody(14f);
            TextRenderer.DrawText(g, a.Icon, iconF,
                new Rectangle(x, y + 8, ActionCellW, 18), t.Text2,
                TextFormatFlags.HorizontalCenter);

            using var idF = t.FontMono(9f);
            TextRenderer.DrawText(g, a.Id, idF,
                new Rectangle(x, y + 28, ActionCellW, 14), t.Text3,
                TextFormatFlags.HorizontalCenter);
        }

        // ── Интерактивность ────────────────────────────────────────────────────
        private void OnMouseClick(object? sender, MouseEventArgs e)
        {
            // Клик по пресету
            int pi = HitTestPreset(e.Location);
            if (pi >= 0 && pi != _activePreset)
            {
                _activePreset = pi;
                LoadPresetSteps(pi);
                Invalidate();
            }
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            _hoveredPreset = HitTestPreset(e.Location) is int pi and >= 0 ? pi : null;
            _hoveredAction = HitTestAction(e.Location);
            Invalidate();
        }

        private int HitTestPreset(Point p)
        {
            int pad = 28;
            int y   = 76;  // pad + titleH + subH
            int x   = pad;
            for (int i = 0; i < _presets.Length; i++)
            {
                var r = new Rectangle(x, y, PresetCardW, PresetCardH);
                if (r.Contains(p)) return i;
                x += PresetCardW + PresetGap;
            }
            return -1;
        }

        private int? HitTestAction(Point p)
        {
            int lx  = Width - LibW - 4;
            int ly  = 28 + 24 + 22;
            int maxRows = (Height - ly - 20) / (ActionCellH + 4);
            int maxShow = Math.Min(Actions.Length, maxRows * ActionCols);

            for (int i = 0; i < maxShow; i++)
            {
                int row = i / ActionCols;
                int col = i % ActionCols;
                int ax  = lx + col * (ActionCellW + 4);
                int ay  = ly + row * (ActionCellH + 4);
                var r   = new Rectangle(ax, ay, ActionCellW, ActionCellH);
                if (r.Contains(p)) return i;
            }
            return null;
        }

        private void LoadPresetSteps(int presetIdx)
        {
            _flowSteps.Clear();
            var steps = _presets[presetIdx].Steps;
            foreach (var step in steps)
            {
                for (int i = 0; i < Actions.Length; i++)
                {
                    if (Actions[i].Id == step) { _flowSteps.Add(i); break; }
                }
            }
        }
    }
}