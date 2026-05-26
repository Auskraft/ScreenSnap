using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenSnap
{
    // ═══════════════════════════════════════════════════════════════════════
    //  EditorForm — редактор скриншотов (Фаза 4)
    //
    //  Инструменты: Select / Arrow / Rect / Ellipse / Line / Text /
    //               Blur / Pixelate / Highlight / Step number
    //  Панель слоёв справа, карточка Yandex Disk снизу справа.
    //
    //  Использование:
    //    EditorForm.Open(imagePath, settings);
    // ═══════════════════════════════════════════════════════════════════════
    public sealed class EditorForm : Form
    {
        // ── Win32 ────────────────────────────────────────────────────────────
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        // ── Tools ─────────────────────────────────────────────────────────────
        public enum Tool
        {
            Select, Arrow, Rect, Ellipse, Line,
            Text, Blur, Pixelate, Highlight, StepNumber
        }

        private static readonly (Tool tool, string icon, string label)[] ToolDefs =
        {
            (Tool.Select,     "↖",  "Select"),
            (Tool.Arrow,      "→",  "Arrow"),
            (Tool.Rect,       "▭",  "Rect"),
            (Tool.Ellipse,    "○",  "Ellipse"),
            (Tool.Line,       "╱",  "Line"),
            (Tool.Text,       "T",  "Text"),
            (Tool.Blur,       "≋",  "Blur"),
            (Tool.Pixelate,   "⊞",  "Pixelate"),
            (Tool.Highlight,  "▬",  "Highlight"),
            (Tool.StepNumber, "①",  "Step"),
        };

        // ── Annotation model ──────────────────────────────────────────────────
        private abstract class Annotation
        {
            public Color Color   { get; set; } = Color.FromArgb(110, 75, 255);
            public float Width   { get; set; } = 2f;
            public abstract void Draw(Graphics g);
            public abstract bool HitTest(Point p);
        }

        private class ArrowAnnotation : Annotation
        {
            public Point From, To;
            public override void Draw(Graphics g)
            {
                using var pen = new Pen(Color, Width) { EndCap = LineCap.ArrowAnchor };
                g.DrawLine(pen, From, To);
            }
            public override bool HitTest(Point p) => DistToSeg(p, From, To) < 8;
        }

        private class RectAnnotation : Annotation
        {
            public Rectangle Bounds;
            public override void Draw(Graphics g)
            {
                using var pen = new Pen(Color, Width);
                g.DrawRectangle(pen, Bounds);
            }
            public override bool HitTest(Point p) => Bounds.Contains(p);
        }

        private class EllipseAnnotation : Annotation
        {
            public Rectangle Bounds;
            public override void Draw(Graphics g)
            {
                using var pen = new Pen(Color, Width);
                g.DrawEllipse(pen, Bounds);
            }
            public override bool HitTest(Point p) => Bounds.Contains(p);
        }

        private class LineAnnotation : Annotation
        {
            public Point From, To;
            public override void Draw(Graphics g)
            {
                using var pen = new Pen(Color, Width);
                g.DrawLine(pen, From, To);
            }
            public override bool HitTest(Point p) => DistToSeg(p, From, To) < 8;
        }

        private class TextAnnotation : Annotation
        {
            public Point Location;
            public string Text = "";
            public override void Draw(Graphics g)
            {
                using var f = FontLoader.GetDisplay(14f, FontStyle.Bold);
                using var b = new SolidBrush(Color);
                g.DrawString(Text, f, b, Location);
            }
            public override bool HitTest(Point p)
                => new Rectangle(Location.X, Location.Y, 120, 24).Contains(p);
        }

        // FIX CS0414: убраны _cache и Invalidate() — Draw() их не использовал
        private class BlurAnnotation : Annotation
        {
            public Rectangle Bounds;
            public override void Draw(Graphics g)
            {
                // Простая имитация blur через масштабирование
                if (Bounds.Width < 2 || Bounds.Height < 2) return;
                using var fill = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
                g.FillRectangle(fill, Bounds);
                using var pen = new Pen(Color.FromArgb(60, 255, 255, 255), 1f);
                for (int x = Bounds.X; x < Bounds.Right; x += 8)
                    g.DrawLine(pen, x, Bounds.Y, x, Bounds.Bottom);
                for (int y = Bounds.Y; y < Bounds.Bottom; y += 8)
                    g.DrawLine(pen, Bounds.X, y, Bounds.Right, y);
            }
            public override bool HitTest(Point p) => Bounds.Contains(p);
        }

        private class PixelateAnnotation : Annotation
        {
            public Rectangle Bounds;
            public override void Draw(Graphics g)
            {
                if (Bounds.Width < 2 || Bounds.Height < 2) return;
                int block = 10;
                using var fill = new SolidBrush(Color.FromArgb(100, 20, 20, 40));
                for (int x = Bounds.X; x < Bounds.Right; x += block)
                    for (int y = Bounds.Y; y < Bounds.Bottom; y += block)
                    {
                        var r = new Rectangle(x, y,
                            Math.Min(block, Bounds.Right - x),
                            Math.Min(block, Bounds.Bottom - y));
                        g.FillRectangle(fill, r);
                        using var pen = new Pen(Color.FromArgb(30, 255, 255, 255), 0.5f);
                        g.DrawRectangle(pen, r);
                    }
            }
            public override bool HitTest(Point p) => Bounds.Contains(p);
        }

        private class HighlightAnnotation : Annotation
        {
            public Rectangle Bounds;
            public override void Draw(Graphics g)
            {
                using var fill = new SolidBrush(Color.FromArgb(90, Color));
                g.FillRectangle(fill, Bounds);
            }
            public override bool HitTest(Point p) => Bounds.Contains(p);
        }

        private class StepAnnotation : Annotation
        {
            public Point Center;
            public int Number;
            public override void Draw(Graphics g)
            {
                var r = new Rectangle(Center.X - 14, Center.Y - 14, 28, 28);
                using var fill = new SolidBrush(Color);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(fill, r);
                using var f = FontLoader.GetDisplay(12f, FontStyle.Bold);
                using var b = new SolidBrush(System.Drawing.Color.White);
                var s = Number.ToString();
                var sz = TextRenderer.MeasureText(s, f);
                g.DrawString(s, f, b,
                    Center.X - sz.Width / 2f + 1,
                    Center.Y - sz.Height / 2f + 1);
            }
            public override bool HitTest(Point p)
                => new Rectangle(Center.X - 14, Center.Y - 14, 28, 28).Contains(p);
        }

        // ── Layout ────────────────────────────────────────────────────────────
        private const int WinW       = 1280;
        private const int WinH       = 820;
        private const int TitleH     = 44;
        private const int ToolbarH   = 52;
        private const int PanelW     = 240;
        private const int StatusH    = 36;
        private const int Radius     = AppTheme.RWindow;
        private const int BtnSize    = 36;

        // ── State ─────────────────────────────────────────────────────────────
        private readonly AppSettings           _settings;
        private readonly string                _imagePath;
        private Bitmap?                        _source;
        private Tool                           _tool       = Tool.Rect;
        private Color                          _color      = Color.FromArgb(110, 75, 255);
        private float                          _strokeW    = 2f;
        private readonly List<Annotation>      _annotations = new();
        private readonly Stack<List<Annotation>> _undo      = new();
        private Annotation?                    _selected;
        private Annotation?                    _drawing;
        private Point                          _dragStart;
        private bool                           _dragging;
        private int                            _stepCounter = 1;
        // FIX CS0649: _yandexLink теперь присваивается в OnRightPanelClick
        private string?                        _yandexLink;

        // ── Panels ────────────────────────────────────────────────────────────
        private readonly Panel  _toolbar;
        private readonly Panel  _canvas;
        private readonly Panel  _rightPanel;
        private readonly Panel  _statusBar;

        // ── Tool buttons ──────────────────────────────────────────────────────
        private readonly Button[] _toolBtns = new Button[ToolDefs.Length];

        // ─────────────────────────────────────────────────────────────────────
        private EditorForm(string imagePath, AppSettings settings)
        {
            _imagePath = imagePath;
            _settings  = settings;

            FormBorderStyle = FormBorderStyle.None;
            Size            = new Size(WinW, WinH);
            StartPosition   = FormStartPosition.CenterScreen;
            BackColor       = Color.Black;
            DoubleBuffered  = true;
            Text            = "Editor — Auskraft Snap";

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);

            // Загружаем изображение
            try { _source = new Bitmap(imagePath); }
            catch { _source = null; }

            // ── Titlebar ──────────────────────────────────────────────────────
            var titlebar = new Panel
            {
                Height    = TitleH,
                Dock      = DockStyle.Top,
                BackColor = Color.Transparent,
            };
            titlebar.Paint      += DrawTitlebar;
            titlebar.MouseDown  += OnTitlebarDrag;

            // Close button
            var btnClose = MakeWinBtn("✕", isClose: true);
            btnClose.Click += (_, _) => Close();
            btnClose.Location = new Point(WinW - 40, (TitleH - 28) / 2);
            titlebar.Controls.Add(btnClose);

            // Save button
            var btnSave = MakeWinBtn("💾 Сохранить", isClose: false, width: 100);
            btnSave.Click += OnSave;
            btnSave.Location = new Point(WinW - 155, (TitleH - 28) / 2);
            titlebar.Controls.Add(btnSave);

            // Copy button
            var btnCopy = MakeWinBtn("📋 Копировать", isClose: false, width: 110);
            btnCopy.Click += OnCopy;
            btnCopy.Location = new Point(WinW - 270, (TitleH - 28) / 2);
            titlebar.Controls.Add(btnCopy);

            // Undo
            var btnUndo = MakeWinBtn("↩ Отменить", isClose: false, width: 100);
            btnUndo.Click += OnUndo;
            btnUndo.Location = new Point(WinW - 375, (TitleH - 28) / 2);
            titlebar.Controls.Add(btnUndo);

            Controls.Add(titlebar);

            // ── Toolbar ───────────────────────────────────────────────────────
            _toolbar = new Panel
            {
                Height    = ToolbarH,
                Dock      = DockStyle.Top,
                BackColor = Color.Transparent,
            };
            _toolbar.Paint += DrawToolbar;
            BuildToolButtons();
            Controls.Add(_toolbar);

            // ── Body ──────────────────────────────────────────────────────────
            var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            // Right panel
            _rightPanel = new Panel
            {
                Width     = PanelW,
                Dock      = DockStyle.Right,
                BackColor = Color.Transparent,
            };
            _rightPanel.Paint       += DrawRightPanel;
            _rightPanel.MouseClick  += OnRightPanelClick; // FIX CS0649: подключаем кнопку загрузки

            // Canvas
            _canvas = new Panel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Cross,
            };
            _canvas.Paint      += DrawCanvas;
            _canvas.MouseDown  += OnCanvasMouseDown;
            _canvas.MouseMove  += OnCanvasMouseMove;
            _canvas.MouseUp    += OnCanvasMouseUp;

            body.Controls.Add(_canvas);
            body.Controls.Add(_rightPanel);
            Controls.Add(body);

            // ── Status bar ────────────────────────────────────────────────────
            _statusBar = new Panel
            {
                Height    = StatusH,
                Dock      = DockStyle.Bottom,
                BackColor = Color.Transparent,
            };
            _statusBar.Paint += DrawStatusBar;
            Controls.Add(_statusBar);

            ThemeManager.ThemeChanged += (_, _) => Invalidate(true);
            ApplyRoundedRegion();
        }

        // ── Public ────────────────────────────────────────────────────────────
        public static void Open(string imagePath, AppSettings settings)
        {
            var form = new EditorForm(imagePath, settings);
            form.Show();
        }

        // ── Tool buttons ──────────────────────────────────────────────────────
        private void BuildToolButtons()
        {
            int x = 12;
            for (int i = 0; i < ToolDefs.Length; i++)
            {
                var (tool, icon, label) = ToolDefs[i];
                int idx = i;
                var btn = new Button
                {
                    Size      = new Size(BtnSize, BtnSize),
                    Location  = new Point(x, (ToolbarH - BtnSize) / 2),
                    Text      = icon,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    ForeColor = Color.FromArgb(180, 245, 247, 255),
                    Font      = FontLoader.GetDisplay(14f),
                    Cursor    = Cursors.Hand,
                    TabStop   = false,
                    Tag       = tool,
                };
                btn.FlatAppearance.BorderSize         = 0;
                btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, 255, 255, 255);
                btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(40, 255, 255, 255);
                btn.Click += (_, _) => SelectTool(tool);
                _toolbar.Controls.Add(btn);
                _toolBtns[i] = btn;
                x += BtnSize + 4;

                // Разделитель после Line
                if (tool == Tool.Line)
                {
                    x += 8;
                    var sep = new Panel
                    {
                        Size      = new Size(1, 28),
                        Location  = new Point(x, (ToolbarH - 28) / 2),
                        BackColor = Color.FromArgb(30, 255, 255, 255),
                    };
                    _toolbar.Controls.Add(sep);
                    x += 9;
                }
            }

            // Цветовые swatches
            x += 12;
            Color[] swatches =
            {
                Color.FromArgb(110, 75, 255),
                Color.FromArgb(255, 84, 112),
                Color.FromArgb(90, 227, 181),
                Color.FromArgb(255, 178, 63),
                Color.FromArgb(0, 212, 255),
                Color.White,
            };
            foreach (var col in swatches)
            {
                var c = col;
                var sw = new Panel
                {
                    Size      = new Size(18, 18),
                    Location  = new Point(x, (ToolbarH - 18) / 2),
                    BackColor = col,
                    Cursor    = Cursors.Hand,
                };
                using var path = DrawingHelpers.RoundedRect(new RectangleF(0, 0, 18, 18), 4f);
                sw.Region = new Region(path);
                sw.Click += (_, _) => { _color = c; _toolbar.Invalidate(); };
                _toolbar.Controls.Add(sw);
                x += 24;
            }

            SelectTool(_tool);
        }

        private void SelectTool(Tool t)
        {
            _tool   = t;
            _canvas.Cursor = t == Tool.Text ? Cursors.IBeam : Cursors.Cross;
            RefreshToolButtons();
            _toolbar.Invalidate();
        }

        private void RefreshToolButtons()
        {
            var theme = ThemeManager.Current;
            for (int i = 0; i < _toolBtns.Length; i++)
            {
                bool active = (Tool)_toolBtns[i].Tag! == _tool;
                _toolBtns[i].ForeColor = active
                    ? theme.Accent.A2
                    : Color.FromArgb(160, 245, 247, 255);
            }
        }

        // ── Draw: Titlebar ────────────────────────────────────────────────────
        private void DrawTitlebar(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var t = ThemeManager.Current;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Фон
            DrawingHelpers.DrawGlassCard(g,
                new RectangleF(0, 0, WinW, TitleH),
                0, t.BgGlassStrong, t.Stroke1);

            // Линия снизу
            using var pen = new Pen(t.Stroke1, 1f);
            g.DrawLine(pen, 0, TitleH - 1, WinW, TitleH - 1);

            // Заголовок
            using var f = t.FontDisplay(FontLoader.BodyM, FontStyle.Bold);
            var name = Path.GetFileName(_imagePath);
            TextRenderer.DrawText(g, $"✏️  {name}", f,
                new Rectangle(16, 0, 500, TitleH), t.Text1,
                TextFormatFlags.VerticalCenter);
        }

        // ── Draw: Toolbar ─────────────────────────────────────────────────────
        private void DrawToolbar(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var t = ThemeManager.Current;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            DrawingHelpers.DrawGlassCard(g,
                new RectangleF(0, 0, _toolbar.Width, ToolbarH),
                0, t.BgGlass, t.Stroke1);

            // Подсветка активного инструмента
            for (int i = 0; i < _toolBtns.Length; i++)
            {
                if ((Tool)_toolBtns[i].Tag! == _tool)
                {
                    var r = new RectangleF(
                        _toolBtns[i].Left - 2, _toolBtns[i].Top - 2,
                        _toolBtns[i].Width + 4, _toolBtns[i].Height + 4);
                    DrawingHelpers.DrawGlassCard(g, r, AppTheme.RXs,
                        Color.FromArgb(30, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B),
                        Color.FromArgb(80, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B));
                    break;
                }
            }

            // Текущий цвет
            using var colorBrush = new SolidBrush(_color);
            using var colorPath  = DrawingHelpers.RoundedRect(
                new RectangleF(_toolbar.Width - PanelW - 52, (ToolbarH - 22) / 2, 22, 22), 4f);
            g.FillPath(colorBrush, colorPath);
            using var borderPen = new Pen(t.Stroke2, 1f);
            g.DrawPath(borderPen, colorPath);
        }

        // ── Draw: Canvas ──────────────────────────────────────────────────────
        private void DrawCanvas(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            var t = ThemeManager.Current;

            // Фон канваса
            g.Clear(t.Bg1);

            // Шахматная сетка (прозрачность)
            DrawCheckerboard(g, _canvas.Width, _canvas.Height);

            if (_source == null)
            {
                using var f = t.FontBody(FontLoader.BodyM);
                TextRenderer.DrawText(g, "Изображение не загружено", f,
                    _canvas.ClientRectangle, t.Text4,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            // Вписываем изображение в канвас с паддингом
            var imgRect = FitImage(_source.Width, _source.Height,
                _canvas.Width, _canvas.Height, padding: 24);

            g.DrawImage(_source, imgRect);

            // Аннотации
            var scale = new PointF(
                (float)imgRect.Width  / _source.Width,
                (float)imgRect.Height / _source.Height);

            var state = g.Save();
            g.TranslateTransform(imgRect.X, imgRect.Y);
            g.ScaleTransform(scale.X, scale.Y);

            foreach (var ann in _annotations)
            {
                if (ann == _selected)
                {
                    var oldG = g.SmoothingMode;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    ann.Draw(g);
                    g.SmoothingMode = oldG;
                }
                else
                {
                    ann.Draw(g);
                }
            }

            // Текущий рисуемый элемент
            _drawing?.Draw(g);

            g.Restore(state);

            // Рамка вокруг канваса
            using var framePen = new Pen(t.Stroke2, 1f);
            g.DrawRectangle(framePen, imgRect.X - 1, imgRect.Y - 1,
                imgRect.Width + 1, imgRect.Height + 1);
        }

        // ── Draw: Right panel ─────────────────────────────────────────────────
        private void DrawRightPanel(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var t = ThemeManager.Current;

            // Фон
            DrawingHelpers.DrawGlassCard(g,
                new RectangleF(0, 0, PanelW, _rightPanel.Height),
                0, t.BgGlass, t.Stroke1);

            int y = 16;

            // ── Заголовок «Layers» ────────────────────────────────────────────
            using var headFont = t.FontDisplay(FontLoader.BodyS, FontStyle.Bold);
            TextRenderer.DrawText(g, "Layers", headFont,
                new Rectangle(16, y, PanelW - 32, 22), t.Text2);
            y += 30;

            // ── Список аннотаций ──────────────────────────────────────────────
            if (_annotations.Count == 0)
            {
                using var emptyFont = t.FontBody(FontLoader.BodyXS);
                TextRenderer.DrawText(g, "Нет объектов", emptyFont,
                    new Rectangle(16, y, PanelW - 32, 20), t.Text4);
                y += 28;
            }
            else
            {
                for (int i = _annotations.Count - 1; i >= 0; i--)
                {
                    var ann  = _annotations[i];
                    bool sel = ann == _selected;
                    var rowR = new RectangleF(8, y, PanelW - 16, 28);

                    DrawingHelpers.DrawGlassCard(g, rowR, AppTheme.RSm,
                        sel ? Color.FromArgb(25, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B)
                            : Color.FromArgb(8, 255, 255, 255),
                        sel ? Color.FromArgb(60, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B)
                            : t.Stroke1);

                    // Цветной квадратик
                    using var cb = new SolidBrush(ann.Color);
                    g.FillRectangle(cb, 16, y + 8, 10, 10);

                    using var rowFont = t.FontBody(FontLoader.BodyXS);
                    TextRenderer.DrawText(g, ann.GetType().Name.Replace("Annotation", ""),
                        rowFont, new Rectangle(32, y + 2, PanelW - 48, 24),
                        sel ? t.Accent.A2 : t.Text2);

                    y += 34;
                    if (y > _rightPanel.Height - 200) break;
                }
            }

            // ── Разделитель ───────────────────────────────────────────────────
            y = _rightPanel.Height - 160;
            using var divPen = new Pen(t.Stroke1, 1f);
            g.DrawLine(divPen, 12, y, PanelW - 12, y);
            y += 12;

            // ── Yandex Disk карточка ──────────────────────────────────────────
            DrawYandexCard(g, t, y);
        }

        private void DrawYandexCard(Graphics g, AppTheme t, int y)
        {
            var cardR = new RectangleF(8, y, PanelW - 16, 130);
            DrawingHelpers.DrawGlassCard(g, cardR, AppTheme.RMd,
                Color.FromArgb(15, t.Yandex.R, t.Yandex.G, t.Yandex.B),
                Color.FromArgb(50, t.Yandex.R, t.Yandex.G, t.Yandex.B));

            int ty = (int)cardR.Y + 12;
            using var titleF = t.FontDisplay(FontLoader.BodyS, FontStyle.Bold);
            TextRenderer.DrawText(g, "☁  Yandex Disk", titleF,
                new Rectangle(16, ty, PanelW - 32, 20), t.Yandex);
            ty += 24;

            if (_yandexLink != null)
            {
                using var linkF = t.FontMono(9f);
                var short_link = _yandexLink.Length > 28
                    ? _yandexLink[..25] + "…"
                    : _yandexLink;
                TextRenderer.DrawText(g, short_link, linkF,
                    new Rectangle(16, ty, PanelW - 32, 16), t.Success);
                ty += 20;
                using var hintF = t.FontBody(FontLoader.BodyXS);
                TextRenderer.DrawText(g, "✓ Загружено", hintF,
                    new Rectangle(16, ty, PanelW - 32, 16), t.Success);
            }
            else
            {
                using var hintF = t.FontBody(FontLoader.BodyXS);
                TextRenderer.DrawText(g, "Нажми «Загрузить»\nчтобы получить ссылку", hintF,
                    new Rectangle(16, ty, PanelW - 32, 36), t.Text3);
                ty += 44;

                // Кнопка загрузки
                var btnR = new RectangleF(16, ty, PanelW - 32, 28);
                DrawingHelpers.DrawGlassCard(g, btnR, AppTheme.RButton,
                    Color.FromArgb(25, t.Yandex.R, t.Yandex.G, t.Yandex.B),
                    Color.FromArgb(80, t.Yandex.R, t.Yandex.G, t.Yandex.B));
                using var btnF = t.FontBody(FontLoader.BodyXS, FontStyle.Bold);
                TextRenderer.DrawText(g, "↑  Загрузить", btnF,
                    new Rectangle(16, ty, PanelW - 32, 28), t.Yandex,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        // FIX CS0649: обработчик клика по правой панели — загрузка на Яндекс.Диск
        private async void OnRightPanelClick(object? sender, MouseEventArgs e)
        {
            if (_yandexLink != null) return; // уже загружено

            // Зона кнопки «Загрузить» внутри карточки
            int cardTop = _rightPanel.Height - 160 + 12;
            int btnTop  = cardTop + 44;
            int btnBot  = btnTop + 28;

            if (e.Y < btnTop || e.Y > btnBot) return; // клик мимо кнопки

            try
            {
                var svc = new YandexDiskService();
                svc.SetToken(_settings.YandexToken);
                _yandexLink = await svc.GetPublicLinkAsync(Path.GetFileName(_imagePath));
                _rightPanel.Invalidate();
            }
            catch (Exception ex)
            {
                ToastManager.Show("Ошибка загрузки", new[] { ex.Message });
            }
        }

        // ── Draw: Status bar ──────────────────────────────────────────────────
        private void DrawStatusBar(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var t = ThemeManager.Current;

            DrawingHelpers.DrawGlassCard(g,
                new RectangleF(0, 0, _statusBar.Width, StatusH),
                0, t.BgGlass, t.Stroke1);

            using var f = t.FontMono(10f);
            var info = _source == null
                ? "Нет изображения"
                : $"{_source.Width} × {_source.Height} px   •   {_annotations.Count} объектов   •   {Path.GetFileName(_imagePath)}";

            TextRenderer.DrawText(g, info, f,
                new Rectangle(16, 0, _statusBar.Width - 32, StatusH),
                t.Text3, TextFormatFlags.VerticalCenter);

            // Инструмент справа
            using var tf = t.FontBody(FontLoader.BodyXS, FontStyle.Bold);
            TextRenderer.DrawText(g, _tool.ToString(), tf,
                new Rectangle(0, 0, _statusBar.Width - 16, StatusH),
                t.Accent.A2,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        // ── Canvas interaction ────────────────────────────────────────────────
        private void OnCanvasMouseDown(object? sender, MouseEventArgs e)
        {
            if (_source == null) return;
            _dragStart = e.Location;
            _dragging  = true;

            // Сохраняем состояние для undo
            var snapshot = new List<Annotation>(_annotations);
            _undo.Push(snapshot);

            if (_tool == Tool.Select)
            {
                _selected = null;
                foreach (var ann in _annotations)
                    if (ann.HitTest(CanvasToImage(e.Location))) { _selected = ann; break; }
                _rightPanel.Invalidate();
                return;
            }

            if (_tool == Tool.Text)
            {
                var loc = CanvasToImage(e.Location);
                var input = Microsoft.VisualBasic.Interaction.InputBox(
                    "Введите текст:", "Text", "");
                if (!string.IsNullOrWhiteSpace(input))
                {
                    _annotations.Add(new TextAnnotation
                    {
                        Location = loc,
                        Text     = input,
                        Color    = _color,
                    });
                    RefreshAll();
                }
                _undo.Pop(); // отменяем пустой snapshot
                return;
            }

            if (_tool == Tool.StepNumber)
            {
                _annotations.Add(new StepAnnotation
                {
                    Center = CanvasToImage(e.Location),
                    Number = _stepCounter++,
                    Color  = _color,
                });
                RefreshAll();
                _undo.Pop();
                return;
            }

            // Начинаем рисование shape
            _drawing = CreateDrawing(e.Location);
        }

        private void OnCanvasMouseMove(object? sender, MouseEventArgs e)
        {
            if (!_dragging || _drawing == null || _source == null) return;
            UpdateDrawing(_drawing, _dragStart, e.Location);
            _canvas.Invalidate();
        }

        private void OnCanvasMouseUp(object? sender, MouseEventArgs e)
        {
            _dragging = false;
            if (_drawing != null)
            {
                _annotations.Add(_drawing);
                _drawing = null;
                RefreshAll();
            }
        }

        private Annotation? CreateDrawing(Point start)
        {
            var p = CanvasToImage(start);
            return _tool switch
            {
                Tool.Arrow     => new ArrowAnnotation     { From = p, To = p, Color = _color, Width = _strokeW },
                Tool.Rect      => new RectAnnotation      { Bounds = new Rectangle(p, Size.Empty), Color = _color, Width = _strokeW },
                Tool.Ellipse   => new EllipseAnnotation   { Bounds = new Rectangle(p, Size.Empty), Color = _color, Width = _strokeW },
                Tool.Line      => new LineAnnotation      { From = p, To = p, Color = _color, Width = _strokeW },
                Tool.Blur      => new BlurAnnotation      { Bounds = new Rectangle(p, Size.Empty), Color = _color },
                Tool.Pixelate  => new PixelateAnnotation  { Bounds = new Rectangle(p, Size.Empty), Color = _color },
                Tool.Highlight => new HighlightAnnotation { Bounds = new Rectangle(p, Size.Empty), Color = _color },
                _ => null,
            };
        }

        private void UpdateDrawing(Annotation ann, Point start, Point end)
        {
            var from = CanvasToImage(start);
            var to   = CanvasToImage(end);
            var rect = NormalizeRect(from, to);

            switch (ann)
            {
                case ArrowAnnotation a:     a.From = from; a.To = to; break;
                case LineAnnotation  l:     l.From = from; l.To = to; break;
                case RectAnnotation  r:     r.Bounds = rect; break;
                case EllipseAnnotation el:  el.Bounds = rect; break;
                case BlurAnnotation b:      b.Bounds = rect; break;
                case PixelateAnnotation px: px.Bounds = rect; break;
                case HighlightAnnotation h: h.Bounds = rect; break;
            }
        }

        // ── Actions ───────────────────────────────────────────────────────────
        private void OnSave(object? sender, EventArgs e)
        {
            if (_source == null) return;
            var bmp = RenderFinal();
            var ext = Path.GetExtension(_imagePath).ToLower();
            var fmt = ext == ".jpg" || ext == ".jpeg" ? ImageFormat.Jpeg : ImageFormat.Png;
            bmp.Save(_imagePath, fmt);
            bmp.Dispose();

            ToastManager.Show("Сохранено", new[] { Path.GetFileName(_imagePath) });
        }

        private void OnCopy(object? sender, EventArgs e)
        {
            if (_source == null) return;
            var bmp = RenderFinal();
            Clipboard.SetImage(bmp);
            ToastManager.Show("Скопировано", new[] { "Изображение в буфере" });
        }

        private void OnUndo(object? sender, EventArgs e)
        {
            if (_undo.Count == 0) return;
            var prev = _undo.Pop();
            _annotations.Clear();
            _annotations.AddRange(prev);
            _selected = null;
            RefreshAll();
        }

        private Bitmap RenderFinal()
        {
            if (_source == null) return new Bitmap(1, 1);
            var bmp = new Bitmap(_source.Width, _source.Height);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawImage(_source, 0, 0);
            foreach (var ann in _annotations) ann.Draw(g);
            return bmp;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private Rectangle FitImage(int imgW, int imgH, int canW, int canH, int padding)
        {
            int availW = canW - padding * 2;
            int availH = canH - padding * 2;
            float scale = Math.Min((float)availW / imgW, (float)availH / imgH);
            int dw = (int)(imgW * scale);
            int dh = (int)(imgH * scale);
            return new Rectangle(
                padding + (availW - dw) / 2,
                padding + (availH - dh) / 2,
                dw, dh);
        }

        private Point CanvasToImage(Point p)
        {
            if (_source == null) return p;
            var imgRect = FitImage(_source.Width, _source.Height,
                _canvas.Width, _canvas.Height, 24);
            float scaleX = (float)_source.Width  / imgRect.Width;
            float scaleY = (float)_source.Height / imgRect.Height;
            return new Point(
                (int)((p.X - imgRect.X) * scaleX),
                (int)((p.Y - imgRect.Y) * scaleY));
        }

        private static Rectangle NormalizeRect(Point a, Point b)
            => new Rectangle(
                Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
                Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

        private static float DistToSeg(Point p, Point a, Point b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            if (dx == 0 && dy == 0) return (float)Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
            float t = Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy)));
            float px = a.X + t * dx - p.X, py = a.Y + t * dy - p.Y;
            return (float)Math.Sqrt(px * px + py * py);
        }

        private static void DrawCheckerboard(Graphics g, int w, int h)
        {
            int cell = 12;
            for (int x = 0; x < w; x += cell)
                for (int y = 0; y < h; y += cell)
                {
                    bool light = ((x / cell + y / cell) % 2 == 0);
                    using var b = new SolidBrush(light
                        ? Color.FromArgb(22, 255, 255, 255)
                        : Color.FromArgb(10, 255, 255, 255));
                    g.FillRectangle(b, x, y, cell, cell);
                }
        }

        private void RefreshAll()
        {
            _canvas.Invalidate();
            _rightPanel.Invalidate();
            _statusBar.Invalidate();
        }

        private static Button MakeWinBtn(string text, bool isClose, int width = 36)
        {
            var t = ThemeManager.Current;
            var btn = new Button
            {
                Text      = text,
                Size      = new Size(width, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = isClose ? Color.FromArgb(255, 84, 112) : Color.FromArgb(160, 245, 247, 255),
                Font      = FontLoader.GetBody(FontLoader.BodyXS),
                Cursor    = Cursors.Hand,
                TabStop   = false,
            };
            btn.FlatAppearance.BorderSize         = 0;
            btn.FlatAppearance.MouseOverBackColor = isClose
                ? Color.FromArgb(30, 255, 84, 112)
                : Color.FromArgb(20, 255, 255, 255);
            btn.FlatAppearance.MouseDownBackColor = btn.FlatAppearance.MouseOverBackColor;
            return btn;
        }

        private void ApplyRoundedRegion()
        {
            using var path = DrawingHelpers.RoundedRect(new RectangleF(0, 0, WinW, WinH), Radius);
            Region = new Region(path);
        }

        private void OnTitlebarDrag(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, 0x2, 0);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _source?.Dispose();
            base.Dispose(disposing);
        }
    }
}