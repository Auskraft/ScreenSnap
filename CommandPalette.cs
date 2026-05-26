using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace ScreenSnap
{
    // ═══════════════════════════════════════════════════════════════════════
    //  CommandPalette — модальное окно Ctrl+K
    //
    //  5 групп команд, live-фильтр, клавиши ↑↓ Enter Esc
    //  Открывается через CommandPalette.Show(owner)
    // ═══════════════════════════════════════════════════════════════════════
    public sealed class CommandPalette : Form
    {
        // ── Команда ──────────────────────────────────────────────────────────
        private sealed record Command(
            string Group,
            string Icon,
            string Title,
            string? Hotkey,
            Action Execute);

        // ── Все команды ───────────────────────────────────────────────────────
        private static readonly Command[] AllCommands =
        {
            // 1. Capture
            new("Capture",   "⚡", "Quick Share",        "Ctrl+Shift+S", () => {}),
            new("Capture",   "✂", "Region Capture",      "Ctrl+Shift+A", () => {}),
            new("Capture",   "🖥", "Active Window",       "Ctrl+Shift+W", () => {}),
            new("Capture",   "🔁", "Repeat Last",         "Ctrl+Shift+R", () => {}),

            // 2. Workflows
            new("Workflows", "⚙", "Open Workflows",      null,           () => {}),
            new("Workflows", "⚡", "Run Quick Share",     null,           () => {}),
            new("Workflows", "✏️", "Run Documentation",   null,           () => {}),
            new("Workflows", "🛡", "Run Privacy Mode",    null,           () => {}),

            // 3. Library
            new("Library",   "🕐", "Open History",        null,           () => {}),
            new("Library",   "📌", "Pinned Captures",     null,           () => {}),
            new("Library",   "🔗", "Shared Captures",     null,           () => {}),

            // 4. Actions
            new("Actions",   "📌", "Pin Last Screenshot", null,           () => {}),
            new("Actions",   "🌙", "Toggle Theme",        null,           () => ThemeManager.Toggle()),

            // 5. Jump
            new("Jump",      "⚙", "Settings",            null,           () => {}),
            new("Jump",      "✏️", "Editor",              null,           () => {}),
            new("Jump",      "🎓", "Replay Onboarding",   null,           () => {}),
        };

        // ── State ─────────────────────────────────────────────────────────────
        private List<Command> _filtered = AllCommands.ToList();
        private int           _selected = 0;
        private string        _query    = "";

        // ── Layout ───────────────────────────────────────────────────────────
        private const int PaletteW  = 640;
        private const int InputH    = 52;
        private const int ItemH     = 40;
        private const int GroupH    = 24;
        private const int MaxVisH   = 420;
        private const int Radius    = AppTheme.RMd;

        private int _scrollOffset = 0; // индекс первого видимого элемента

        // ── Конструктор ───────────────────────────────────────────────────────
        public CommandPalette()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition   = FormStartPosition.Manual;
            BackColor       = Color.Black;
            DoubleBuffered  = true;
            ShowInTaskbar   = false;
            TopMost         = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);

            KeyPreview = true;
            KeyDown   += OnKeyDown;
            KeyPress  += OnKeyPress;
            MouseClick += OnMouseClick;
            MouseMove  += OnMouseMove;
            Deactivate += (_, _) => Close();

            UpdateSize();
            ApplyRoundedRegion();
        }

        // ── Публичный вход ────────────────────────────────────────────────────
        public static void Open(Form owner)
        {
            var palette = new CommandPalette();

            palette.Location = new Point(
                owner.Left + (owner.Width  - PaletteW) / 2,
                owner.Top  + (owner.Height - palette.Height) / 3);

            palette.Show(owner);
            palette.BringToFront();
        }

        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var t = ThemeManager.Current;

            // Фон
            DrawingHelpers.DrawGlassCard(g,
                new RectangleF(0, 0, Width, Height),
                Radius, t.BgGlassStrong, t.Stroke2);

            // Поле ввода
            DrawInput(g, t);

            // Разделитель
            using var div = new Pen(t.Stroke1, 1f);
            g.DrawLine(div, 16, InputH, Width - 16, InputH);

            // Список команд
            DrawList(g, t);

            // Подсказка внизу
            DrawFooter(g, t);
        }

        // ── Input ─────────────────────────────────────────────────────────────
        private void DrawInput(Graphics g, AppTheme t)
        {
            // Иконка поиска
            using var iconFont = t.FontBody(16f);
            TextRenderer.DrawText(g, "⌕", iconFont,
                new Rectangle(18, 14, 24, 24), t.Text3);

            // Запрос или placeholder
            using var inputFont = t.FontBody(FontLoader.BodyM);
            var text     = _query.Length > 0 ? _query : "Поиск команд, workflows, истории…";
            var color    = _query.Length > 0 ? t.Text1 : t.Text3;
            TextRenderer.DrawText(g, text, inputFont,
                new Rectangle(48, 15, Width - 100, 24), color,
                TextFormatFlags.VerticalCenter);

            // Esc hint
            using var kbdFont = t.FontMono(10f);
            var kbdRect = new Rectangle(Width - 52, 18, 40, 16);
            DrawingHelpers.DrawGlassCard(g, kbdRect, AppTheme.RXs,
                Color.FromArgb(12, 255, 255, 255), t.Stroke2);
            TextRenderer.DrawText(g, "Esc", kbdFont, kbdRect, t.Text3,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // ── List ──────────────────────────────────────────────────────────────
        private void DrawList(Graphics g, AppTheme t)
        {
            if (_filtered.Count == 0)
            {
                using var ef = t.FontBody(FontLoader.BodyS);
                TextRenderer.DrawText(g, "Ничего не найдено", ef,
                    new Rectangle(0, InputH + 20, Width, 40), t.Text4,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            int y        = InputH + 4;
            int maxY     = Height - 32; // оставляем место под футер

            // Группируем отфильтрованные команды
            var groups = _filtered
                .GroupBy(c => c.Group)
                .ToList();

            int visualIdx = 0; // абсолютный индекс в _filtered
            int drawIdx   = 0; // для скролла

            foreach (var group in groups)
            {
                // Заголовок группы
                if (drawIdx >= _scrollOffset)
                {
                    if (y + GroupH > maxY) break;
                    using var gf = t.FontBody(10f, FontStyle.Bold);
                    TextRenderer.DrawText(g, group.Key.ToUpper(), gf,
                        new Rectangle(20, y, Width - 40, GroupH), t.Text4,
                        TextFormatFlags.VerticalCenter);
                    y += GroupH;
                }
                drawIdx++;

                foreach (var cmd in group)
                {
                    if (drawIdx > _scrollOffset)
                    {
                        if (y + ItemH > maxY) goto DoneDrawing;
                        DrawItem(g, t, cmd, visualIdx, y);
                        y += ItemH;
                    }
                    drawIdx++;
                    visualIdx++;
                }
            }
            DoneDrawing:;
        }

        private void DrawItem(Graphics g, AppTheme t, Command cmd, int idx, int y)
        {
            bool selected = _selected == idx;
            var rect      = new RectangleF(8, y, Width - 16, ItemH - 2);

            if (selected)
            {
                using var path  = DrawingHelpers.RoundedRect(rect, AppTheme.RSm);
                using var brush = new SolidBrush(
                    Color.FromArgb(20, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B));
                g.FillPath(brush, path);
                using var pen = new Pen(
                    Color.FromArgb(50, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B), 1f);
                g.DrawPath(pen, path);
            }

            // Иконка
            using var iconFont = t.FontBody(13f);
            TextRenderer.DrawText(g, cmd.Icon, iconFont,
                new Rectangle(20, y, 22, ItemH), selected ? t.Accent.A2 : t.Text3,
                TextFormatFlags.VerticalCenter);

            // Название
            using var titleFont = t.FontBody(FontLoader.BodyS,
                selected ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(g, cmd.Title, titleFont,
                new Rectangle(48, y, Width - 160, ItemH),
                selected ? t.Text1 : t.Text2,
                TextFormatFlags.VerticalCenter);

            // Хоткей
            if (cmd.Hotkey != null)
            {
                using var hkFont = t.FontMono(10f);
                var hkSize = TextRenderer.MeasureText(cmd.Hotkey, hkFont);
                var hkRect = new Rectangle(Width - hkSize.Width - 24, y + (ItemH - 18) / 2,
                    hkSize.Width + 16, 18);
                DrawingHelpers.DrawGlassCard(g, hkRect, AppTheme.RXs,
                    Color.FromArgb(10, 255, 255, 255), t.Stroke2);
                TextRenderer.DrawText(g, cmd.Hotkey, hkFont, hkRect, t.Text3,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        // ── Footer ────────────────────────────────────────────────────────────
        private void DrawFooter(Graphics g, AppTheme t)
        {
            int fy = Height - 28;
            using var pen = new Pen(t.Stroke1, 0.5f);
            g.DrawLine(pen, 16, fy, Width - 16, fy);

            using var f = t.FontMono(10f);
            var hints = new[] { ("↑↓", "навигация"), ("Enter", "выбрать"), ("Esc", "закрыть") };
            int x = 20;
            foreach (var (key, hint) in hints)
            {
                var kr = new Rectangle(x, fy + 6, TextRenderer.MeasureText(key, f).Width + 10, 16);
                DrawingHelpers.DrawGlassCard(g, kr, AppTheme.RXs,
                    Color.FromArgb(10, 255, 255, 255), t.Stroke2);
                TextRenderer.DrawText(g, key, f, kr, t.Text3,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                x += kr.Width + 4;

                using var hf = t.FontBody(10f);
                TextRenderer.DrawText(g, hint, hf,
                    new Rectangle(x, fy + 6, 80, 16), t.Text4);
                x += 88;
            }
        }

        // ── Keyboard ──────────────────────────────────────────────────────────
        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    Close();
                    e.Handled = true;
                    break;

                case Keys.Up:
                    if (_selected > 0) { _selected--; EnsureVisible(); Invalidate(); }
                    e.Handled = true;
                    break;

                case Keys.Down:
                    if (_selected < _filtered.Count - 1) { _selected++; EnsureVisible(); Invalidate(); }
                    e.Handled = true;
                    break;

                case Keys.Enter:
                    ExecuteSelected();
                    e.Handled = true;
                    break;

                case Keys.Back:
                    if (_query.Length > 0)
                    {
                        _query = _query[..^1];
                        Filter();
                    }
                    e.Handled = true;
                    break;
            }
        }

        private void OnKeyPress(object? sender, KeyPressEventArgs e)
        {
            if (e.KeyChar >= 32) // печатаемые символы
            {
                _query += e.KeyChar;
                Filter();
                e.Handled = true;
            }
        }

        // ── Mouse ─────────────────────────────────────────────────────────────
        private void OnMouseClick(object? sender, MouseEventArgs e)
        {
            int? idx = HitTestItem(e.Location);
            if (idx.HasValue)
            {
                _selected = idx.Value;
                ExecuteSelected();
            }
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            int? idx = HitTestItem(e.Location);
            if (idx.HasValue && idx.Value != _selected)
            {
                _selected = idx.Value;
                Invalidate();
            }
        }

        private int? HitTestItem(Point p)
        {
            int y = InputH + 4;
            int visualIdx = 0;

            foreach (var group in _filtered.GroupBy(c => c.Group))
            {
                y += GroupH; // заголовок группы
                foreach (var cmd in group)
                {
                    var r = new Rectangle(8, y, Width - 16, ItemH);
                    if (r.Contains(p)) return visualIdx;
                    y += ItemH;
                    visualIdx++;
                }
            }
            return null;
        }

        // ── Filter & Execute ──────────────────────────────────────────────────
        private void Filter()
        {
            var q = _query.Trim().ToLowerInvariant();
            _filtered = q.Length == 0
                ? AllCommands.ToList()
                : AllCommands
                    .Where(c => c.Title.ToLowerInvariant().Contains(q) ||
                                c.Group.ToLowerInvariant().Contains(q))
                    .ToList();

            _selected     = 0;
            _scrollOffset = 0;
            UpdateSize();
            ApplyRoundedRegion();
            Invalidate();
        }

        private void ExecuteSelected()
        {
            if (_selected >= 0 && _selected < _filtered.Count)
            {
                var cmd = _filtered[_selected];
                Close();
                cmd.Execute();
            }
        }

        private void EnsureVisible()
        {
            // Простой скролл: если выбранный вышел за экран — двигаем offset
            // (упрощённо, без учёта GroupH — достаточно для текущего объёма)
            const int visibleItems = (MaxVisH - 32) / ItemH;
            if (_selected < _scrollOffset)
                _scrollOffset = _selected;
            else if (_selected >= _scrollOffset + visibleItems)
                _scrollOffset = _selected - visibleItems + 1;
        }

        // ── Size & Region ─────────────────────────────────────────────────────
        private void UpdateSize()
        {
            // Считаем нужную высоту
            int itemCount  = _filtered.Count;
            int groupCount = _filtered.Select(c => c.Group).Distinct().Count();
            int listH      = itemCount * ItemH + groupCount * GroupH + 8;
            int totalH     = InputH + Math.Min(listH, MaxVisH - InputH) + 32;

            Size = new Size(PaletteW, Math.Max(totalH, InputH + 80));
        }

        private void ApplyRoundedRegion()
        {
            using var path = DrawingHelpers.RoundedRect(
                new RectangleF(0, 0, Width, Height), Radius);
            Region = new Region(path);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}