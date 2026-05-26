using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ScreenSnap
{
    // ═══════════════════════════════════════════════════════════════════════
    //  HistoryScreen — экран истории скриншотов
    //
    //  Содержит:
    //    • Hero-карточка (слоган)
    //    • Фильтры: All / Uploaded / Local only / Pinned
    //    • Строка поиска → открывает CommandPalette
    //    • Плитки скриншотов 3 колонки
    // ═══════════════════════════════════════════════════════════════════════
    public sealed class HistoryScreen : UserControl
    {
        // ── События ──────────────────────────────────────────────────────────
        /// Просят открыть Command Palette (фаза 3)
        public event EventHandler? SearchRequested;

        // ── Модель данных ─────────────────────────────────────────────────────
        public enum FilterMode { All, Uploaded, LocalOnly, Pinned }

        public sealed class ScreenshotItem
        {
            public string   FilePath  { get; init; } = "";
            public string   FileName  { get; init; } = "";
            public DateTime Taken     { get; init; }
            public long     Bytes     { get; init; }
            public bool     Uploaded  { get; init; }
            public bool     Pinned    { get; init; }
            public Image?   Thumb     { get; set; }   // загружается лениво
        }

        private readonly List<ScreenshotItem> _allItems  = new();
        private List<ScreenshotItem>          _filtered  = new();
        private FilterMode                    _filter    = FilterMode.All;
        private int?                          _hovered   = null;

        // ── Layout ───────────────────────────────────────────────────────────
        private const int PadX      = 28;
        private const int HeroH     = 82;
        private const int FilterH   = 32;
        private const int SearchH   = 32;
        private const int GridCols  = 3;
        private const int TileH     = 148;
        private const int TileGapX  = 12;
        private const int TileGapY  = 12;

        // Скролл
        private int _scrollY = 0;

        // ─────────────────────────────────────────────────────────────────────
        public HistoryScreen()
        {
            Dock           = DockStyle.Fill;
            BackColor      = Color.Transparent;
            DoubleBuffered = true;

            SetStyle(ControlStyles.AllPaintingInWmPaint  |
                     ControlStyles.OptimizedDoubleBuffer  |
                     ControlStyles.UserPaint              |
                     ControlStyles.ResizeRedraw, true);

            MouseClick += OnMouseClick;
            MouseMove  += OnMouseMove;
            MouseWheel += (_, e) =>
            {
                _scrollY = Math.Max(0, _scrollY - e.Delta);
                Invalidate();
            };

            ThemeManager.ThemeChanged += (_, _) => Invalidate();
        }

        // ── Public API ────────────────────────────────────────────────────────
        /// Загрузить скриншоты из папки
        public void LoadFolder(string folder)
        {
            _allItems.Clear();
            if (!Directory.Exists(folder)) { ApplyFilter(); Invalidate(); return; }

            var exts = new[] { ".png", ".jpg", ".jpeg", ".webp" };
            var files = Directory.GetFiles(folder)
                .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .Take(120);

            foreach (var fp in files)
            {
                var fi = new FileInfo(fp);
                _allItems.Add(new ScreenshotItem
                {
                    FilePath = fp,
                    FileName = fi.Name,
                    Taken    = fi.LastWriteTime,
                    Bytes    = fi.Length,
                    Uploaded = false,
                    Pinned   = false,
                });
            }

            ApplyFilter();
            Invalidate();
        }

        // Добавить один новый снимок (вызывается из TrayContext после захвата)
        public void AddItem(ScreenshotItem item)
        {
            _allItems.Insert(0, item);
            ApplyFilter();
            Invalidate();
        }

        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var t   = ThemeManager.Current;
            int y   = PadX - _scrollY;

            // ── Hero ─────────────────────────────────────────────────────────
            DrawHero(g, t, PadX, y);
            y += HeroH + 20;

            // ── Toolbar: фильтры + поиск ──────────────────────────────────────
            DrawToolbar(g, t, PadX, y);
            y += FilterH + 20;

            // ── Пустое состояние ──────────────────────────────────────────────
            if (_filtered.Count == 0)
            {
                DrawEmpty(g, t, y);
                return;
            }

            // ── Плитки ───────────────────────────────────────────────────────
            DrawGrid(g, t, PadX, y);
        }

        // ── Hero ──────────────────────────────────────────────────────────────
        private void DrawHero(Graphics g, AppTheme t, int x, int y)
        {
            var rect = new RectangleF(x, y, Width - x * 2, HeroH);

            // Стеклянная карточка
            DrawingHelpers.DrawGlassCard(g, rect, AppTheme.RMd, t.BgGlass, t.Stroke1);

            // Акцентный gradient strip слева
            using var stripBrush = new LinearGradientBrush(
                new RectangleF(x, y, 4, HeroH),
                t.Accent.A1, t.Accent.A3, 90f);
            g.FillRectangle(stripBrush, x + 1, y + 12, 3, HeroH - 24);

            // Слоган
            using var headFont = t.FontDisplay(FontLoader.DisplayS, FontStyle.Bold);
            TextRenderer.DrawText(g, "Capture → Upload → Link.", headFont,
                new Rectangle(x + 20, y + 14, (int)rect.Width - 40, 28), t.Text1);

            using var subFont = t.FontBody(FontLoader.BodyS);
            TextRenderer.DrawText(g, "One keystroke.", subFont,
                new Rectangle(x + 20, y + 44, 200, 20),
                Color.FromArgb(200, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B));

            // Кол-во снимков справа
            using var cntFont = t.FontDisplay(22f, FontStyle.Bold);
            var cntText  = _allItems.Count.ToString();
            var cntSize  = TextRenderer.MeasureText(cntText, cntFont);
            TextRenderer.DrawText(g, cntText, cntFont,
                new Rectangle((int)(rect.Right - cntSize.Width - 24), y + 14,
                    cntSize.Width + 10, 30), t.Text1);

            using var cntSubFont = t.FontBody(10f);
            TextRenderer.DrawText(g, "скриншотов", cntSubFont,
                new Rectangle((int)(rect.Right - 78), y + 46, 70, 14), t.Text3,
                TextFormatFlags.HorizontalCenter);
        }

        // ── Toolbar ───────────────────────────────────────────────────────────
        private static readonly (FilterMode Mode, string Label)[] Filters =
        {
            (FilterMode.All,       "All"),
            (FilterMode.Uploaded,  "Uploaded"),
            (FilterMode.LocalOnly, "Local only"),
            (FilterMode.Pinned,    "Pinned"),
        };

        private void DrawToolbar(Graphics g, AppTheme t, int x, int y)
        {
            int cx = x;

            // Фильтры
            foreach (var (mode, label) in Filters)
            {
                bool active = _filter == mode;
                using var f = active
                    ? t.FontBody(FontLoader.BodyXS, FontStyle.Bold)
                    : t.FontBody(FontLoader.BodyXS);
                var sz  = TextRenderer.MeasureText(label, f);
                int fw  = sz.Width + 20;
                var r   = new Rectangle(cx, y, fw, FilterH);

                Color fill   = active
                    ? Color.FromArgb(20, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B)
                    : Color.FromArgb(6, 255, 255, 255);
                Color stroke = active
                    ? Color.FromArgb(80, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B)
                    : t.Stroke1;

                DrawingHelpers.DrawGlassCard(g, r, AppTheme.RButton, fill, stroke);
                TextRenderer.DrawText(g, label, f, r,
                    active ? t.Accent.A2 : t.Text2,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                cx += fw + 6;
            }

            // Поиск — справа
            int sw   = 200;
            int sx   = Width - PadX - sw;
            var srect = new Rectangle(sx, y, sw, SearchH);
            DrawingHelpers.DrawGlassCard(g, srect, AppTheme.RInput,
                Color.FromArgb(8, 255, 255, 255), t.Stroke1);

            using var sf = t.FontBody(FontLoader.BodyXS);
            TextRenderer.DrawText(g, "⌕  Поиск…", sf, srect, t.Text4,
                TextFormatFlags.VerticalCenter);

            using var kbdF = t.FontMono(10f);
            var kbdR = new Rectangle(sx + sw - 52, y + 8, 44, 16);
            DrawingHelpers.DrawGlassCard(g, kbdR, 4f,
                Color.FromArgb(10, 255, 255, 255), t.Stroke2);
            TextRenderer.DrawText(g, "Ctrl+K", kbdF, kbdR, t.Text3,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        // ── Grid ──────────────────────────────────────────────────────────────
        private void DrawGrid(Graphics g, AppTheme t, int startX, int startY)
        {
            int gridW   = Width - startX * 2;
            int tileW   = (gridW - TileGapX * (GridCols - 1)) / GridCols;

            for (int i = 0; i < _filtered.Count; i++)
            {
                int col = i % GridCols;
                int row = i / GridCols;
                int x   = startX + col * (tileW + TileGapX);
                int y   = startY + row * (TileH  + TileGapY);

                if (y > Height + _scrollY) break;  // вне экрана — не рисуем

                DrawTile(g, t, _filtered[i], i, x, y, tileW);
            }
        }

        private void DrawTile(Graphics g, AppTheme t, ScreenshotItem item,
            int idx, int x, int y, int tileW)
        {
            bool hovered = _hovered == idx;
            var rect     = new RectangleF(x, y, tileW, TileH);

            Color fill   = hovered
                ? Color.FromArgb(14, 255, 255, 255)
                : t.BgGlass;
            DrawingHelpers.DrawGlassCard(g, rect, AppTheme.RSm, fill, t.Stroke1);

            // Превью-зона (верхние 96px)
            var thumbRect = new Rectangle(x + 1, y + 1, tileW - 2, 96);
            if (item.Thumb != null)
            {
                g.SetClip(new RectangleF(thumbRect.X, thumbRect.Y,
    thumbRect.Width, thumbRect.Height));
                g.DrawImage(item.Thumb, thumbRect);
                g.ResetClip();
            }
            else
            {
                // Заглушка: тёмный прямоугольник
                using var ph = new SolidBrush(Color.FromArgb(20, 255, 255, 255));
                g.FillRectangle(ph, thumbRect);

                var ext = Path.GetExtension(item.FileName).ToUpperInvariant();
                using var ef  = t.FontMono(12f);
                TextRenderer.DrawText(g, ext, ef, thumbRect, t.Text4,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                // Асинхронно загружаем thumbnail
                if (!string.IsNullOrEmpty(item.FilePath))
                    LoadThumbAsync(item);
            }

            // Имя файла
            int metaY = y + 102;
            using var nameFont = t.FontBody(10f);
            var nameTrunc = TruncateName(item.FileName, 28);
            TextRenderer.DrawText(g, nameTrunc, nameFont,
                new Rectangle(x + 8, metaY, tileW - 16, 16), t.Text2);
            metaY += 18;

            // Дата + размер
            using var metaFont = t.FontMono(9f);
            var dateStr = item.Taken.ToString("dd.MM.yy  HH:mm");
            var sizeStr = FormatBytes(item.Bytes);
            TextRenderer.DrawText(g, dateStr, metaFont,
                new Rectangle(x + 8, metaY, tileW / 2, 14), t.Text4);
            TextRenderer.DrawText(g, sizeStr, metaFont,
                new Rectangle(x + tileW / 2, metaY, tileW / 2 - 8, 14), t.Text4,
                TextFormatFlags.Right);

            // Uploaded badge
            if (item.Uploaded)
            {
                var br = new Rectangle(x + tileW - 54, y + 6, 46, 16);
                using var bb = new SolidBrush(Color.FromArgb(20, t.Success.R, t.Success.G, t.Success.B));
                using var bp = DrawingHelpers.RoundedRect(br, 4f);
                g.FillPath(bb, bp);
                using var uf = t.FontMono(9f);
                TextRenderer.DrawText(g, "↑ cloud", uf, br, t.Success,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // Pinned badge
            if (item.Pinned)
            {
                using var pinF = t.FontBody(11f);
                TextRenderer.DrawText(g, "📌", pinF,
                    new Rectangle(x + 6, y + 4, 18, 18), t.Text2);
            }
        }

        // ── Empty state ───────────────────────────────────────────────────────
        private void DrawEmpty(Graphics g, AppTheme t, int y)
        {
            using var f = t.FontBody(FontLoader.BodyS);
            TextRenderer.DrawText(g, "Скриншотов пока нет.", f,
                new Rectangle(PadX, y + 40, Width - PadX * 2, 28), t.Text4,
                TextFormatFlags.HorizontalCenter);

            using var sf = t.FontBody(FontLoader.BodyXS);
            TextRenderer.DrawText(g, "Нажми Ctrl+Shift+A чтобы сделать первый.", sf,
                new Rectangle(PadX, y + 72, Width - PadX * 2, 20), t.Text4,
                TextFormatFlags.HorizontalCenter);
        }

        // ── Filter ────────────────────────────────────────────────────────────
        private void ApplyFilter()
        {
            _filtered = _filter switch
            {
                FilterMode.Uploaded  => _allItems.Where(i => i.Uploaded).ToList(),
                FilterMode.LocalOnly => _allItems.Where(i => !i.Uploaded).ToList(),
                FilterMode.Pinned    => _allItems.Where(i => i.Pinned).ToList(),
                _                    => _allItems.ToList(),
            };
        }

        // ── Interaction ───────────────────────────────────────────────────────
        private void OnMouseClick(object? sender, MouseEventArgs e)
        {
            // Фильтры
            int filterIdx = HitTestFilter(e.Location);
            if (filterIdx >= 0)
            {
                _filter = Filters[filterIdx].Mode;
                _scrollY = 0;
                ApplyFilter();
                Invalidate();
                return;
            }

            // Поиск
            if (HitTestSearch(e.Location))
            {
                SearchRequested?.Invoke(this, EventArgs.Empty);
                return;
            }
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            _hovered = HitTestTile(e.Location);
            Invalidate();
        }

        private int HitTestFilter(Point p)
        {
            int y  = PadX + HeroH + 20;
            int cx = PadX;
            for (int i = 0; i < Filters.Length; i++)
            {
                using var f = t_FontHack(Filters[i].Mode == _filter);
                var sz = TextRenderer.MeasureText(Filters[i].Label, f);
                int fw = sz.Width + 20;
                if (new Rectangle(cx, y, fw, FilterH).Contains(p)) return i;
                cx += fw + 6;
            }
            return -1;
        }

        private bool HitTestSearch(Point p)
        {
            int y  = PadX + HeroH + 20;
            int sw = 200;
            int sx = Width - PadX - sw;
            return new Rectangle(sx, y, sw, SearchH).Contains(p);
        }

        private int? HitTestTile(Point p)
        {
            int startX = PadX;
            int startY = PadX + HeroH + 20 + FilterH + 20 - _scrollY;
            int gridW  = Width - startX * 2;
            int tileW  = (gridW - TileGapX * (GridCols - 1)) / GridCols;

            for (int i = 0; i < _filtered.Count; i++)
            {
                int col = i % GridCols;
                int row = i / GridCols;
                int x   = startX + col * (tileW + TileGapX);
                int y   = startY + row * (TileH  + TileGapY);
                if (new Rectangle(x, y, tileW, TileH).Contains(p)) return i;
            }
            return null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        // Мини-хак для HitTest без GDI-объекта
        private Font t_FontHack(bool bold) =>
            ThemeManager.Current.FontBody(FontLoader.BodyXS,
                bold ? FontStyle.Bold : FontStyle.Regular);

        private static string TruncateName(string name, int max) =>
            name.Length <= max ? name : name[..(max - 1)] + "…";

        private static string FormatBytes(long bytes) => bytes switch
        {
            < 1024        => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024} KB",
            _             => $"{bytes / (1024 * 1024):F1} MB",
        };

        private async void LoadThumbAsync(ScreenshotItem item)
        {
            try
            {
                var bmp = await System.Threading.Tasks.Task.Run(() =>
                {
                    using var full = Image.FromFile(item.FilePath);
                    var thumb = new Bitmap(240, 135);
                    using var gThumb = Graphics.FromImage(thumb);
                    gThumb.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    gThumb.DrawImage(full, 0, 0, 240, 135);
                    return thumb;
                });

                if (!IsDisposed)
                {
                    item.Thumb = bmp;
                    BeginInvoke(Invalidate);
                }
            }
            catch { /* файл недоступен — оставляем заглушку */ }
        }
    }
}