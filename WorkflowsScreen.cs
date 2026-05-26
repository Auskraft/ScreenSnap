using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ScreenSnap
{
    // ═══════════════════════════════════════════════════════════════════════
    //  WorkflowsScreen — Фаза 2 (переработан: layout, palette, no emoji)
    // ═══════════════════════════════════════════════════════════════════════
    public sealed class WorkflowsScreen : UserControl
    {
        // ── Пресеты ──────────────────────────────────────────────────────────
        private record Preset(string Name, string Hotkey, string[] Steps, Color Tint);

        private readonly Preset[] _presets;
        private int   _activePreset  = 0;
        private int?  _hoveredPreset = null;
        private int?  _hoveredAction = null;

        // ── Actions library ───────────────────────────────────────────────────
        private record ActionDef(string Id, string Label, bool IsNew = false);

        private static readonly ActionDef[] Actions =
        {
            new("trigger",  "trigger"),
            new("copy",     "copy"),
            new("save",     "save"),
            new("editor",   "editor"),
            new("upload",   "upload"),
            new("link",     "link"),
            new("open",     "open"),
            new("qr",       "qr"),
            new("pin",      "pin"),
            new("folder",   "folder"),
            new("notify",   "notify"),
            new("blur",     "blur"),
            new("compress", "compress", true),
            new("steps",    "steps",    true),
            new("share",    "share",    true),
            new("private",  "private",  true),
            new("markdown", "MD",       true),
        };

        // ── Иконки действий (Unicode Segoe MDL2 / простые символы) ──────────
        // Рисуем сами через GDI+ — без emoji
        private static readonly Dictionary<string, string> ActionSymbols = new()
        {
            ["trigger"]  = "⊙",
            ["copy"]     = "⧉",
            ["save"]     = "▣",
            ["editor"]   = "✐",
            ["upload"]   = "↑",
            ["link"]     = "⊕",
            ["open"]     = "◉",
            ["qr"]       = "▦",
            ["pin"]      = "⊛",
            ["folder"]   = "▤",
            ["notify"]   = "◔",
            ["blur"]     = "◌",
            ["compress"] = "◈",
            ["steps"]    = "#",
            ["share"]    = "↗",
            ["private"]  = "◑",
            ["markdown"] = "M",
        };

        // ── Flow steps (индексы в Actions[]) ─────────────────────────────────
        private List<int> _flowSteps = new();

        // ── Layout constants ──────────────────────────────────────────────────
        private const int PadH        = 28;   // горизонтальный отступ
        private const int PadV        = 24;   // вертикальный отступ сверху
        private const int PresetCardW = 182;
        private const int PresetCardH = 76;
        private const int PresetGap   = 8;
        private const int LibW        = 184;  // ширина правой панели
        private const int LibPadOuter = 10;   // отступ панели от правого края
        private const int LibPadInner = 14;   // внутренний отступ панели
        private const int CellW       = 72;
        private const int CellH       = 60;
        private const int CellGap     = 6;
        private const int CellCols    = 2;

        // ─────────────────────────────────────────────────────────────────────
        public WorkflowsScreen()
        {
            Dock           = DockStyle.Fill;
            BackColor      = Color.Transparent;
            DoubleBuffered = true;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint            |
                ControlStyles.ResizeRedraw, true);

            _presets = new[]
            {
                new Preset("Quick Share",       "Ctrl+Shift+S",
                    new[] { "trigger","compress","upload","share" },
                    Color.FromArgb(110, 75, 255)),
                new Preset("Documentation Mode","Ctrl+Shift+D",
                    new[] { "trigger","editor","steps","save" },
                    Color.FromArgb(60, 200, 130)),
                new Preset("Privacy Mode",      "Ctrl+Shift+P",
                    new[] { "trigger","blur","upload","private","link" },
                    Color.FromArgb(70, 150, 255)),
                new Preset("Save & Pin",        "Ctrl+Shift+T",
                    new[] { "trigger","save","pin" },
                    Color.FromArgb(255, 160, 60)),
            };

            LoadPresetSteps(0);

            MouseClick += OnMouseClick;
            MouseMove  += OnMouseMove;
            ThemeManager.ThemeChanged += (_, _) => Invalidate();
        }

        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var t = ThemeManager.Current;

            // Область контента (слева от правой панели)
            int contentRight = Width - LibW - LibPadOuter * 2 - PadH;

            // ── Правая панель — рисуем первой (фон) ──────────────────────────
            DrawActionsLibrary(g, t, contentRight);

            // ── Заголовок ────────────────────────────────────────────────────
            int y = PadV;

            using var titleFont = t.FontDisplay(FontLoader.DisplayM, FontStyle.Bold);
            TextRenderer.DrawText(g, "Workflows", titleFont,
                new Rectangle(PadH, y, contentRight - PadH, 44),
                t.Text1,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            y += 48;

            using var subFont = t.FontBody(FontLoader.BodyXS);
            TextRenderer.DrawText(g,
                "Автоматизируй скриншоты. Запусти пресет или собери свой flow.",
                subFont,
                new Rectangle(PadH, y, contentRight - PadH, 20),
                t.Text3,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            y += 30;

            // ── Пресет-карточки ───────────────────────────────────────────────
            DrawPresetRow(g, t, y);
            y += PresetCardH + 22;

            // ── Разделитель + заголовок flow ──────────────────────────────────
            using var divPen = new Pen(t.Stroke1, 0.5f);
            g.DrawLine(divPen, PadH, y, contentRight, y);
            y += 14;

            using var flowLabelFont = t.FontBody(FontLoader.BodyXS, FontStyle.Bold);
            TextRenderer.DrawText(g, "FLOW BUILDER", flowLabelFont,
                new Rectangle(PadH, y, 110, 16), t.Text3);

            var presetName = _presets[_activePreset].Name;
            using var pNameFont = t.FontBody(FontLoader.BodyXS);
            TextRenderer.DrawText(g, $"— {presetName}", pNameFont,
                new Rectangle(PadH + 116, y, 220, 16), t.Text4);
            y += 26;

            // ── Flow builder ──────────────────────────────────────────────────
            DrawFlowBuilder(g, t, y, contentRight - PadH);
        }

        // ── Пресет-карточки ───────────────────────────────────────────────────
        private void DrawPresetRow(Graphics g, AppTheme t, int y)
        {
            int x = PadH;
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

            Color fill = active
                ? Color.FromArgb(28, p.Tint.R, p.Tint.G, p.Tint.B)
                : hovered
                    ? Color.FromArgb(12, 255, 255, 255)
                    : t.BgGlass;

            Color stroke = active
                ? Color.FromArgb(90, p.Tint.R, p.Tint.G, p.Tint.B)
                : hovered
                    ? t.Stroke2
                    : t.Stroke1;

            DrawingHelpers.DrawGlassCard(g, rect, AppTheme.RSm, fill, stroke);

            // Акцент-полоска сверху для активного
            if (active)
            {
                using var stripBrush = new SolidBrush(
                    Color.FromArgb(200, p.Tint.R, p.Tint.G, p.Tint.B));
                using var gp = new GraphicsPath();
                gp.AddRoundedRectangle(new RectangleF(x + 14, y, PresetCardW - 28, 2), 1f);
                g.FillPath(stripBrush, gp);
            }

            // Символ-иконка (не emoji)
            string sym = idx == 0 ? "↯"
                       : idx == 1 ? "✐"
                       : idx == 2 ? "◑"
                       :            "⊛";

            using var symFont = new Font("Segoe UI Symbol", 15f, FontStyle.Regular, GraphicsUnit.Point);
            TextRenderer.DrawText(g, sym, symFont,
                new Rectangle(x + 14, y + 10, 24, 24),
                active ? Color.FromArgb(220, p.Tint.R, p.Tint.G, p.Tint.B) : t.Text2);

            // Название
            using var nameFont = t.FontBody(FontLoader.BodyS,
                active ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(g, p.Name, nameFont,
                new Rectangle(x + 44, y + 10, PresetCardW - 54, 18),
                active ? t.Text1 : t.Text2);

            // Хоткей
            using var hkFont = t.FontMono(10f);
            TextRenderer.DrawText(g, p.Hotkey, hkFont,
                new Rectangle(x + 44, y + 30, PresetCardW - 54, 14),
                active ? Color.FromArgb(180, p.Tint.R, p.Tint.G, p.Tint.B) : t.Text4);

            // Шаги (точки)
            int dotX = x + 14;
            int dotY = y + PresetCardH - 18;
            foreach (var step in _presets[idx].Steps)
            {
                bool isTrigger = step == "trigger";
                var dotColor = active
                    ? Color.FromArgb(isTrigger ? 200 : 100, p.Tint.R, p.Tint.G, p.Tint.B)
                    : t.Stroke2;
                using var dotBrush = new SolidBrush(dotColor);
                g.FillEllipse(dotBrush, dotX, dotY, 5, 5);
                dotX += 9;
            }
        }

        // ── Flow builder ──────────────────────────────────────────────────────
        private void DrawFlowBuilder(Graphics g, AppTheme t, int y, int maxWidth)
        {
            var acc = _presets[_activePreset].Tint;

            if (_flowSteps.Count == 0)
            {
                using var ef = t.FontBody(FontLoader.BodyXS);
                TextRenderer.DrawText(g, "Пресет не содержит шагов", ef,
                    new Rectangle(PadH, y, maxWidth, 60), t.Text4,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            // Фон canvas
            var canvasRect = new RectangleF(PadH, y, maxWidth, 80);
            using var canvasBrush = new SolidBrush(Color.FromArgb(8, 255, 255, 255));
            using var canvasPath = DrawingHelpers.RoundedRect(
                new Rectangle(PadH, y, maxWidth, 80), AppTheme.RSm);
            g.FillPath(canvasBrush, canvasPath);
            using var canvasPen = new Pen(t.Stroke1, 0.5f);
            g.DrawPath(canvasPen, canvasPath);

            int blockW = 108;
            int blockH = 52;
            int arrowW = 28;
            int cx     = PadH + 18;
            int by     = y + (80 - blockH) / 2;

            foreach (int ai in _flowSteps)
            {
                var action = Actions[ai];
                bool isTrigger = action.Id == "trigger";

                Color blockFill   = isTrigger
                    ? Color.FromArgb(30, acc.R, acc.G, acc.B)
                    : Color.FromArgb(14, 255, 255, 255);
                Color blockStroke = isTrigger
                    ? Color.FromArgb(80, acc.R, acc.G, acc.B)
                    : t.Stroke2;

                var blockRect = new RectangleF(cx, by, blockW, blockH);
                DrawingHelpers.DrawGlassCard(g, blockRect, AppTheme.RSm, blockFill, blockStroke);

                // Иконка-контейнер 22x22
                var iconBox = new Rectangle(cx + 10, by + 8, 22, 22);
                Color iconBg = isTrigger
                    ? Color.FromArgb(50, acc.R, acc.G, acc.B)
                    : Color.FromArgb(28, 110, 75, 255);
                Color iconFg = isTrigger
                    ? Color.FromArgb(220, acc.R, acc.G, acc.B)
                    : Color.FromArgb(200, 140, 110, 255);

                using var ibPath = DrawingHelpers.RoundedRect(iconBox, 5f);
                using var ibBrush = new SolidBrush(iconBg);
                g.FillPath(ibBrush, ibPath);
                using var ibPen = new Pen(Color.FromArgb(60, iconFg.R, iconFg.G, iconFg.B), 0.5f);
                g.DrawPath(ibPen, ibPath);

                string sym = ActionSymbols.GetValueOrDefault(action.Id, "·");
                using var symF = new Font("Segoe UI Symbol", 10f, FontStyle.Regular, GraphicsUnit.Point);
                TextRenderer.DrawText(g, sym, symF, iconBox, iconFg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                // Название
                using var idF = t.FontMono(10f);
                TextRenderer.DrawText(g, action.Id, idF,
                    new Rectangle(cx + 10, by + 32, blockW - 14, 14), t.Text3);

                // Бейдж NEW
                if (action.IsNew)
                {
                    var nr = new Rectangle(cx + blockW - 30, by + 6, 26, 12);
                    using var nb = new SolidBrush(Color.FromArgb(22, acc.R, acc.G, acc.B));
                    using var np = DrawingHelpers.RoundedRect(nr, 3f);
                    g.FillPath(nb, np);
                    using var npen = new Pen(Color.FromArgb(70, acc.R, acc.G, acc.B), 0.5f);
                    g.DrawPath(npen, np);
                    using var nf = t.FontMono(8f);
                    TextRenderer.DrawText(g, "NEW", nf, nr,
                        Color.FromArgb(200, acc.R, acc.G, acc.B),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }

                cx += blockW;

                bool isLast = ai == _flowSteps[^1];
                if (!isLast && cx + arrowW + blockW <= PadH + maxWidth)
                {
                    int ay = by + blockH / 2;
                    using var arrowPen = new Pen(Color.FromArgb(110, acc.R, acc.G, acc.B), 1.5f)
                    {
                        CustomEndCap = new AdjustableArrowCap(3.5f, 3.5f)
                    };
                    g.DrawLine(arrowPen, cx, ay, cx + arrowW, ay);
                    cx += arrowW;
                }

                if (cx + blockW > PadH + maxWidth) break;
            }
        }

        // ── Actions Library (правая панель) ────────────────────────────────────
        private void DrawActionsLibrary(Graphics g, AppTheme t, int contentRight)
        {
            // Фон панели — от верха до низа UserControl
            int panelX = contentRight + PadH;
            int panelW = Width - panelX - LibPadOuter;
            if (panelW < 80 || Height < 60) return;

            var panelRect = new RectangleF(panelX, 0, panelW, Height);
            DrawingHelpers.DrawGlassCard(g, panelRect, 0f, t.BgGlass, t.Stroke1);

            // Вертикальная линия-разделитель слева
            using var sepPen = new Pen(t.Stroke1, 0.5f);
            g.DrawLine(sepPen, panelX, 0, panelX, Height);

            int lx = panelX + LibPadInner;
            int ly = PadV;

            // Заголовок
            using var hFont = t.FontBody(FontLoader.BodyXS, FontStyle.Bold);
            TextRenderer.DrawText(g, "ACTIONS", hFont,
                new Rectangle(lx, ly, panelW - LibPadInner, 16), t.Text3);
            ly += 20;

            using var subF = t.FontBody(10f);
            TextRenderer.DrawText(g, "Перетащи в flow builder", subF,
                new Rectangle(lx, ly, panelW - LibPadInner, 14), t.Text4);
            ly += 22;

            // Сетка ячеек
            int usableW  = panelW - LibPadInner * 2;
            int cellW    = (usableW - CellGap) / CellCols;

            for (int i = 0; i < Actions.Length; i++)
            {
                int row = i / CellCols;
                int col = i % CellCols;
                int ax  = lx + col * (cellW + CellGap);
                int ay  = ly + row * (CellH + CellGap);

                if (ay + CellH > Height - 8) break;

                DrawActionCell(g, t, Actions[i], i, ax, ay, cellW);
            }
        }

        private void DrawActionCell(Graphics g, AppTheme t, ActionDef a, int idx,
            int x, int y, int w)
        {
            bool hovered = _hoveredAction == idx;
            bool isNew   = a.IsNew;
            var  acc     = t.Accent;

            Color fill = hovered
                ? Color.FromArgb(18, acc.A2.R, acc.A2.G, acc.A2.B)
                : Color.FromArgb(8,  255, 255, 255);
            Color stroke = hovered
                ? Color.FromArgb(70, acc.A2.R, acc.A2.G, acc.A2.B)
                : t.Stroke1;

            var rect = new RectangleF(x, y, w, CellH);
            DrawingHelpers.DrawGlassCard(g, rect, AppTheme.RXs, fill, stroke);

            // Иконка-контейнер 26x26 по центру сверху
            int iconBoxX = x + (w - 26) / 2;
            int iconBoxY = y + 8;
            var iconBox  = new Rectangle(iconBoxX, iconBoxY, 26, 26);

            Color iconBg = hovered
                ? Color.FromArgb(50, acc.A2.R, acc.A2.G, acc.A2.B)
                : Color.FromArgb(22, acc.A2.R, acc.A2.G, acc.A2.B);
            Color iconFg = hovered
                ? Color.FromArgb(240, acc.A2.R, acc.A2.G, acc.A2.B)
                : Color.FromArgb(190, 140, 110, 255);

            using var ibPath  = DrawingHelpers.RoundedRect(iconBox, 6f);
            using var ibBrush = new SolidBrush(iconBg);
            g.FillPath(ibBrush, ibPath);
            using var ibPen = new Pen(Color.FromArgb(50, iconFg.R, iconFg.G, iconFg.B), 0.5f);
            g.DrawPath(ibPen, ibPath);

            string sym = ActionSymbols.GetValueOrDefault(a.Id, "·");
            using var symF = new Font("Segoe UI Symbol", 11f, FontStyle.Regular, GraphicsUnit.Point);
            TextRenderer.DrawText(g, sym, symF, iconBox, iconFg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // Название
            using var idF = t.FontMono(9f);
            TextRenderer.DrawText(g, a.Label, idF,
                new Rectangle(x, y + CellH - 18, w, 14), t.Text3,
                TextFormatFlags.HorizontalCenter);

            // Бейдж NEW
            if (isNew)
            {
                var nr = new Rectangle(x + w - 20, y + 4, 16, 9);
                using var nb  = new SolidBrush(Color.FromArgb(20, acc.A2.R, acc.A2.G, acc.A2.B));
                using var np  = DrawingHelpers.RoundedRect(nr, 2f);
                g.FillPath(nb, np);
                using var nf  = t.FontMono(7.5f);
                TextRenderer.DrawText(g, "NEW", nf, nr, acc.A2,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        // ── HitTest ───────────────────────────────────────────────────────────
        private int HitTestPreset(Point p)
        {
            // y = PadV + 48 (title) + 30 (sub)
            int y = PadV + 48 + 30;
            int x = PadH;
            for (int i = 0; i < _presets.Length; i++)
            {
                if (new Rectangle(x, y, PresetCardW, PresetCardH).Contains(p)) return i;
                x += PresetCardW + PresetGap;
            }
            return -1;
        }

        private int? HitTestAction(Point p)
        {
            int contentRight = Width - LibW - LibPadOuter * 2 - PadH;
            int panelX = contentRight + PadH;
            int lx = panelX + LibPadInner;
            int ly = PadV + 20 + 22;
            int panelW = Width - panelX - LibPadOuter;
            if (panelW < 80) return null;

            int cellW = (panelW - LibPadInner * 2 - CellGap) / CellCols;

            for (int i = 0; i < Actions.Length; i++)
            {
                int row = i / CellCols;
                int col = i % CellCols;
                int ax  = lx + col * (cellW + CellGap);
                int ay  = ly + row * (CellH + CellGap);
                if (ay + CellH > Height - 8) break;
                if (new Rectangle(ax, ay, cellW, CellH).Contains(p)) return i;
            }
            return null;
        }

        // ── События ───────────────────────────────────────────────────────────
        private void OnMouseClick(object? sender, MouseEventArgs e)
        {
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
            int pi = HitTestPreset(e.Location);
            _hoveredPreset = pi >= 0 ? pi : null;
            _hoveredAction = HitTestAction(e.Location);
            Invalidate();
        }

        private void LoadPresetSteps(int presetIdx)
        {
            _flowSteps.Clear();
            foreach (var step in _presets[presetIdx].Steps)
                for (int i = 0; i < Actions.Length; i++)
                    if (Actions[i].Id == step) { _flowSteps.Add(i); break; }
        }
    }

    // ── Extension для GraphicsPath.AddRoundedRectangle ────────────────────────
    internal static class GraphicsPathExtensions
    {
        public static void AddRoundedRectangle(this GraphicsPath path, RectangleF rect, float radius)
        {
            if (radius <= 0) { path.AddRectangle(rect); return; }
            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
        }
    }
}