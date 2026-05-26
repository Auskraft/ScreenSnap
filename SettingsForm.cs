using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ScreenSnap
{
    // ═══════════════════════════════════════════════════════════════════════
    //  SettingsForm v2 — рестайл под ThemeManager
    //
    //  9 вкладок: General / Hotkeys / Appearance / Storage / Cloud /
    //             Editor / Privacy / Workflows / About
    //
    //  Изменения vs v1:
    //    ✅ Дизайн-система ThemeManager (стекло, акценты, шрифты)
    //    ✅ Вкладки-таблеты вместо стандартного TabControl
    //    ✅ Хоткей Command Palette → Ctrl+K
    //    ❌ Убрана строка OCR (Ctrl+Shift+O)
    // ═══════════════════════════════════════════════════════════════════════
    public sealed class SettingsForm : Form
    {
        // ── Зависимости ───────────────────────────────────────────────────────
        private readonly AppSettings _settings;

        // ── Вкладки ───────────────────────────────────────────────────────────
        private enum Tab
        {
            General, Hotkeys, Appearance, Storage,
            Cloud, Editor, Privacy, Workflows, About
        }

        private static readonly string[] TabLabels =
        {
            "General", "Hotkeys", "Appearance", "Storage",
            "Cloud", "Editor", "Privacy", "Workflows", "About"
        };

        private Tab   _activeTab  = Tab.General;
        private Tab?  _hoveredTab = null;

        // ── Контролы контента ─────────────────────────────────────────────────
        // Словарь: вкладка → набор её контролов (показываем/прячем при переключении)
        private readonly Dictionary<Tab, List<Control>> _tabControls = new();
        private readonly Panel _contentPanel;

        // ── General controls ──────────────────────────────────────────────────
        private TextBox _txtSavePath = null!;
        private ComboBox _cmbFormat  = null!;
        private CheckBox _chkAutoStart = null!;
        private CheckBox _chkClipboard = null!;

        // ── Hotkey controls ───────────────────────────────────────────────────
        private readonly List<(string Label, string SettingKey, TextBox Box)> _hotkeys = new();

        // ── Appearance controls ───────────────────────────────────────────────
        private readonly List<(AccentPalette Palette, Rectangle Rect)> _accentRects = new();

        // ── Cloud controls ────────────────────────────────────────────────────
        private TextBox _txtYandexToken = null!;
        private CheckBox _chkAutoUpload = null!;

        // ── Layout ───────────────────────────────────────────────────────────
        private const int TabBarH   = 40;
        private const int TabBarPad = 12;
        private const int FormW     = 680;
        private const int FormH     = 520;

        // ─────────────────────────────────────────────────────────────────────
        public SettingsForm(AppSettings settings)
        {
            _settings = settings;

            // ── Form ─────────────────────────────────────────────────────────
            Text            = "Настройки — Auskraft Snap";
            FormBorderStyle = FormBorderStyle.None;
            Size            = new Size(FormW, FormH);
            StartPosition   = FormStartPosition.CenterParent;
            DoubleBuffered  = true;
            BackColor       = Color.Black;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);

            // Drag
            MouseDown += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessageW(Handle, 0xA1, 0x2, 0);
                }
            };

            // ── Tab bar ──────────────────────────────────────────────────────
            // Отрисовывается в OnPaint, клики — в OnMouseClick

            // ── Content panel ────────────────────────────────────────────────
            _contentPanel = new Panel
            {
                Bounds    = new Rectangle(0, TabBarH + 1, FormW, FormH - TabBarH - 1),
                BackColor = Color.Transparent,
            };
            Controls.Add(_contentPanel);

            // Кнопка закрытия
            var btnClose = new Button
            {
                Text      = "✕",
                Size      = new Size(28, 24),
                Location  = new Point(FormW - 32, 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(150, 255, 255, 255),
                Cursor    = Cursors.Hand,
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (_, _) => Close();
            Controls.Add(btnClose);

            // Строим контролы всех вкладок
            BuildGeneralTab();
            BuildHotkeysTab();
            BuildAppearanceTab();
            BuildStorageTab();
            BuildCloudTab();
            BuildStubTab(Tab.Editor,    "Редактор изображений появится в Фазе 4.");
            BuildStubTab(Tab.Privacy,   "Настройки приватности появятся в Фазе 3.");
            BuildStubTab(Tab.Workflows, "Управление workflow появится в Фазе 3.");
            BuildAboutTab();

            LoadValues();
            ShowTab(_activeTab);

            ThemeManager.ThemeChanged += (_, _) => { ApplyTheme(); Invalidate(); };
            ApplyTheme();

            MouseClick += OnMouseClick;
            MouseMove  += OnMouseMove;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessageW(IntPtr hWnd, int Msg, int wParam, int lParam);

        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var t = ThemeManager.Current;

            // Окно
            var winRect = new RectangleF(0, 0, Width, Height);
            DrawingHelpers.DrawGlassCard(g, winRect, AppTheme.RLg, t.BgGlassStrong, t.Stroke2);

            // Tab bar background
            using var tbBrush = new SolidBrush(Color.FromArgb(8, 255, 255, 255));
            g.FillRectangle(tbBrush, 0, 0, Width, TabBarH);

            // Разделитель
            using var divPen = new Pen(t.Stroke1, 0.5f);
            g.DrawLine(divPen, 0, TabBarH, Width, TabBarH);

            // Таблеты вкладок
            int tx = TabBarPad;
            for (int i = 0; i < TabLabels.Length; i++)
            {
                var tab = (Tab)i;
                DrawTabPill(g, t, tab, tx, (TabBarH - 26) / 2);
                tx += MeasureTabWidth(t, TabLabels[i]) + 4;
            }
        }

        private void DrawTabPill(Graphics g, AppTheme t, Tab tab, int x, int y)
        {
            bool active  = _activeTab  == tab;
            bool hovered = _hoveredTab == tab;
            var  label   = TabLabels[(int)tab];

            using var f  = active
                ? t.FontBody(FontLoader.BodyXS, FontStyle.Bold)
                : t.FontBody(FontLoader.BodyXS);
            int w        = MeasureTabWidth(t, label);
            var r        = new Rectangle(x, y, w, 26);

            Color fill;
            Color stroke;
            Color textColor;

            if (active)
            {
                fill      = Color.FromArgb(22, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B);
                stroke    = Color.FromArgb(70, t.Accent.A2.R, t.Accent.A2.G, t.Accent.A2.B);
                textColor = t.Accent.A2;
            }
            else if (hovered)
            {
                fill      = Color.FromArgb(10, 255, 255, 255);
                stroke    = t.Stroke1;
                textColor = t.Text2;
            }
            else
            {
                fill      = Color.FromArgb(0, 0, 0, 0); // полностью прозрачный через ARGB
                stroke    = Color.FromArgb(0, 0, 0, 0);
                textColor = t.Text3;
            }

            if (fill.A > 0)
                DrawingHelpers.DrawGlassCard(g, r, AppTheme.RSm, fill, stroke);

            TextRenderer.DrawText(g, label, f, r, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private int MeasureTabWidth(AppTheme t, string label)
        {
            using var f = t.FontBody(FontLoader.BodyXS, FontStyle.Bold);
            return TextRenderer.MeasureText(label, f).Width + 16;
        }

        // ── Tab switching ─────────────────────────────────────────────────────
        private void ShowTab(Tab tab)
        {
            _activeTab = tab;
            foreach (var kvp in _tabControls)
            {
                bool show = kvp.Key == tab;
                foreach (var c in kvp.Value)
                    c.Visible = show;
            }
            Invalidate();
        }

        private void ApplyTheme()
        {
            if (InvokeRequired) { Invoke(ApplyTheme); return; }
            var t = ThemeManager.Current;
            _contentPanel.BackColor = Color.Transparent;
            foreach (Control c in _contentPanel.Controls)
                StyleControl(c, t);
            Invalidate(true);
        }

        // ── FIX: разделены ветки для каждого типа контрола ────────────────────
        private static void StyleControl(Control c, AppTheme t)
        {
            // CheckBox не поддерживает Color.Transparent — используем почти-прозрачный
            if (c is CheckBox chk)
            {
                chk.BackColor = Color.FromArgb(1, 0, 0, 0);
                chk.ForeColor = t.Text1;
                return;
            }

            // RadioButton — аналогично
            if (c is RadioButton rb)
            {
                rb.BackColor = Color.FromArgb(1, 0, 0, 0);
                rb.ForeColor = t.Text1;
                return;
            }

            // TextBox
            if (c is TextBox tb)
            {
                tb.BorderStyle = BorderStyle.FixedSingle;
                tb.BackColor   = Color.FromArgb(20, 20, 28);
                tb.ForeColor   = t.Text1;
                return;
            }

            // ComboBox не поддерживает Transparent
            if (c is ComboBox cb)
            {
                cb.BackColor = Color.FromArgb(20, 20, 28);
                cb.ForeColor = t.Text1;
                return;
            }

            // Button
            if (c is Button btn)
            {
                btn.BackColor = Color.Transparent;
                btn.ForeColor = btn.Tag is "primary" ? t.Accent.A2 : t.Text1;
                return;
            }

            // Всё остальное (Label, Panel и т.д.) — Transparent безопасен
            c.BackColor = Color.Transparent;
            c.ForeColor = t.Text1;
        }

        // ── Builders ──────────────────────────────────────────────────────────
        private void BuildGeneralTab()
        {
            var controls = new List<Control>();
            int y        = 20;
            int pad      = 24;

            controls.Add(SectionLabel("Папка сохранения", pad, y)); y += 22;
            _txtSavePath = StyledTextBox(pad, y, FormW - pad * 2 - 80);
            var btnBrowse = AccentButton("Обзор", FormW - pad - 74, y, 70);
            btnBrowse.Click += (_, _) =>
            {
                using var dlg = new FolderBrowserDialog
                    { SelectedPath = _txtSavePath.Text };
                if (dlg.ShowDialog() == DialogResult.OK)
                    _txtSavePath.Text = dlg.SelectedPath;
            };
            controls.Add(_txtSavePath);
            controls.Add(btnBrowse);
            y += 38;

            controls.Add(SectionLabel("Формат файла", pad, y)); y += 22;
            _cmbFormat = new ComboBox
            {
                Bounds        = new Rectangle(pad, y, 120, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor     = Color.FromArgb(20, 20, 28),
                ForeColor     = Color.White,
            };
            _cmbFormat.Items.AddRange(new[] { "PNG", "JPG", "WEBP" });
            controls.Add(_cmbFormat);
            y += 44;

            _chkAutoStart = StyledCheckBox("Запускать при старте Windows", pad, y);
            controls.Add(_chkAutoStart);
            y += 32;

            _chkClipboard = StyledCheckBox("Копировать в буфер обмена после захвата", pad, y);
            controls.Add(_chkClipboard);
            y += 48;

            controls.Add(SaveButton(pad, y));

            AddTabControls(Tab.General, controls);
        }

        private void BuildHotkeysTab()
        {
            var controls = new List<Control>();
            int y        = 20;
            int pad      = 24;
            int labelW   = 240;
            int boxW     = 180;

            controls.Add(SectionLabel("Горячие клавиши", pad, y)); y += 22;

            var rows = new[]
            {
                ("Полный экран",       "hotkeyFullScreen"),
                ("Выделить область",   "hotkeyRegion"),
                ("Quick Share",        "hotkeyQuickShare"),
                ("Command Palette",    "hotkeyCommandPalette"),
                ("Documentation Mode", ""),
                ("Privacy Mode",       ""),
                ("Save & Pin",         ""),
                ("Active window",      ""),
                ("Repeat last",        ""),
            };

            foreach (var (label, key) in rows)
            {
                var lbl = SectionLabel(label, pad, y + 4);
                lbl.Width = labelW;
                controls.Add(lbl);

                if (!string.IsNullOrEmpty(key))
                {
                    var box = new TextBox
                    {
                        Bounds      = new Rectangle(pad + labelW + 12, y, boxW, 26),
                        ReadOnly    = true,
                        Tag         = key,
                        BackColor   = Color.FromArgb(20, 20, 28),
                        ForeColor   = Color.White,
                        BorderStyle = BorderStyle.FixedSingle,
                    };
                    box.KeyDown += (_, e) => CaptureHotkey(e, box);
                    controls.Add(box);
                    _hotkeys.Add((label, key, box));
                }
                else
                {
                    var ph = SectionLabel("Ctrl+Shift+? (Фаза 3)", pad + labelW + 12, y + 4);
                    ph.ForeColor = Color.FromArgb(80, 255, 255, 255);
                    controls.Add(ph);
                }

                y += 34;
            }

            y += 8;
            controls.Add(SaveButton(pad, y));

            AddTabControls(Tab.Hotkeys, controls);
        }

        private void BuildAppearanceTab()
        {
            var controls = new List<Control>();
            int y        = 20;
            int pad      = 24;

            controls.Add(SectionLabel("Тема", pad, y)); y += 22;

            var btnDark  = AccentButton("🌙  Тёмная",  pad,       y, 110);
            var btnLight = AccentButton("☀  Светлая",  pad + 116, y, 110);
            btnDark.Click  += (_, _) => ThemeManager.SetMode(AppThemeMode.Dark);
            btnLight.Click += (_, _) => ThemeManager.SetMode(AppThemeMode.Light);
            controls.Add(btnDark);
            controls.Add(btnLight);
            y += 44;

            controls.Add(SectionLabel("Акцентный цвет", pad, y)); y += 22;

            var accentDefs = new[]
            {
                (AccentPalette.Violet,  Color.FromArgb(110, 75, 255),  "Violet"),
                (AccentPalette.Cyan,    Color.FromArgb(0, 200, 255),    "Cyan"),
                (AccentPalette.Magenta, Color.FromArgb(220, 80, 255),   "Magenta"),
                (AccentPalette.Lime,    Color.FromArgb(100, 230, 170),  "Lime"),
            };

            _accentRects.Clear();
            int ax = pad;
            foreach (var (palette, color, name) in accentDefs)
            {
                _accentRects.Add((palette, new Rectangle(ax, y, 40, 40)));
                ax += 52;
            }

            var accentPanel = new Panel
            {
                Bounds    = new Rectangle(pad, y, 250, 50),
                BackColor = Color.Transparent,
            };
            accentPanel.Paint += (_, pe) => DrawAccentPicker(pe.Graphics, accentDefs, pad, y);
            accentPanel.MouseClick += (_, me) =>
            {
                int lx  = me.X;
                int idx = lx / 52;
                if (idx >= 0 && idx < accentDefs.Length)
                    ThemeManager.SetAccent(accentDefs[idx].Item1);
            };
            controls.Add(accentPanel);

            AddTabControls(Tab.Appearance, controls);
        }

        private void BuildStorageTab()
        {
            var controls = new List<Control>();
            int y        = 20;
            int pad      = 24;

            controls.Add(SectionLabel("Хранилище", pad, y)); y += 28;

            var info = SectionLabel(
                "Папка хранения настраивается во вкладке General.\n" +
                "Здесь можно очистить кэш превью или открыть папку.", pad, y);
            info.AutoSize = true;
            controls.Add(info);
            y += 60;

            var btnOpen = AccentButton("📂  Открыть папку", pad, y, 150);
            btnOpen.Click += (_, _) =>
            {
                try { System.Diagnostics.Process.Start("explorer.exe", _settings.SaveFolder); }
                catch { }
            };
            controls.Add(btnOpen);

            AddTabControls(Tab.Storage, controls);
        }

        private void BuildCloudTab()
        {
            var controls = new List<Control>();
            int y        = 20;
            int pad      = 24;

            controls.Add(SectionLabel("Яндекс.Диск — токен WebDAV", pad, y)); y += 22;

            var hint = SectionLabel(
                "Яндекс ID → Безопасность → Пароли приложений", pad, y);
            hint.ForeColor = Color.FromArgb(100, 200, 200, 200);
            controls.Add(hint);
            y += 22;

            _txtYandexToken = StyledTextBox(pad, y, FormW - pad * 2);
            _txtYandexToken.UseSystemPasswordChar = false;
            controls.Add(_txtYandexToken);
            y += 44;

            _chkAutoUpload = StyledCheckBox("Автоматически загружать после захвата", pad, y);
            controls.Add(_chkAutoUpload);
            y += 48;

            controls.Add(SaveButton(pad, y));

            AddTabControls(Tab.Cloud, controls);
        }

        private void BuildAboutTab()
        {
            var controls = new List<Control>();
            int pad      = 24;

            var lbl = new Label
            {
                Text      = "Auskraft Snap v2\n\nФаза 2 — Основные экраны\n\n" +
                             "Разработано на C# / WinForms + GDI+\n" +
                             "Дизайн: Claude Design\n\n" +
                             "GitHub: github.com/Auskraft/ScreenSnap",
                Bounds    = new Rectangle(pad, 24, FormW - pad * 2, 200),
                AutoSize  = false,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(180, 245, 247, 255),
            };
            controls.Add(lbl);

            AddTabControls(Tab.About, controls);
        }

        private void BuildStubTab(Tab tab, string message)
        {
            var lbl = new Label
            {
                Text      = message,
                Bounds    = new Rectangle(24, 60, FormW - 48, 40),
                AutoSize  = false,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(80, 245, 247, 255),
            };
            AddTabControls(tab, new List<Control> { lbl });
        }

        // ── Load / Save values ────────────────────────────────────────────────
        private void LoadValues()
        {
            _txtSavePath.Text       = _settings.SaveFolder;
            _cmbFormat.SelectedItem = _settings.FileFormat.ToUpperInvariant();
            if (_cmbFormat.SelectedIndex < 0) _cmbFormat.SelectedIndex = 0;
            _chkAutoStart.Checked   = IsAutostartEnabled();
            _chkClipboard.Checked   = _settings.CopyToClipboard;
            _txtYandexToken.Text    = _settings.YandexToken;
            _chkAutoUpload.Checked  = _settings.AutoUpload;

            foreach (var (_, key, box) in _hotkeys)
            {
                box.Text = key switch
                {
                    "hotkeyFullScreen"     => _settings.HotkeyFullScreen,
                    "hotkeyRegion"         => _settings.HotkeyRegion,
                    "hotkeyQuickShare"     => _settings.HotkeyQuickShare,
                    "hotkeyCommandPalette" => _settings.HotkeyCommandPalette,
                    _                      => "",
                };
            }
        }

        private void SaveAll()
        {
            _settings.SaveFolder      = _txtSavePath.Text;
            _settings.FileFormat      = (_cmbFormat.SelectedItem?.ToString() ?? "PNG").ToLowerInvariant();
            _settings.CopyToClipboard = _chkClipboard.Checked;
            _settings.YandexToken     = _txtYandexToken.Text;
            _settings.AutoUpload      = _chkAutoUpload.Checked;

            foreach (var (_, key, box) in _hotkeys)
            {
                switch (key)
                {
                    case "hotkeyFullScreen":     _settings.HotkeyFullScreen     = box.Text; break;
                    case "hotkeyRegion":         _settings.HotkeyRegion         = box.Text; break;
                    case "hotkeyQuickShare":     _settings.HotkeyQuickShare     = box.Text; break;
                    case "hotkeyCommandPalette": _settings.HotkeyCommandPalette = box.Text; break;
                }
            }

            SetAutostart(_chkAutoStart.Checked);
            _settings.Save();
            Close();
        }

        // ── Hotkey capture ────────────────────────────────────────────────────
        private static void CaptureHotkey(KeyEventArgs e, TextBox box)
        {
            e.SuppressKeyPress = true;
            var parts = new List<string>();
            if (e.Control) parts.Add("Ctrl");
            if (e.Shift)   parts.Add("Shift");
            if (e.Alt)     parts.Add("Alt");

            if (e.KeyCode is not (Keys.ControlKey or Keys.ShiftKey or Keys.Menu))
                parts.Add(e.KeyCode.ToString());

            if (parts.Count >= 2)
                box.Text = string.Join("+", parts);
        }

        // ── Accent picker paint ────────────────────────────────────────────────
        private void DrawAccentPicker(Graphics g, (AccentPalette, Color, string)[] defs, int baseX, int baseY)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int ax = 0;
            foreach (var (palette, color, _) in defs)
            {
                bool active = ThemeManager.Accent == palette;
                using var b = new SolidBrush(color);
                g.FillEllipse(b, ax, 4, 32, 32);
                if (active)
                {
                    using var p = new Pen(Color.White, 2.5f);
                    g.DrawEllipse(p, ax - 2, 2, 36, 36);
                }
                ax += 52;
            }
        }

        // ── Interaction ───────────────────────────────────────────────────────
        private void OnMouseClick(object? sender, MouseEventArgs e)
        {
            var tab = HitTestTab(e.Location);
            if (tab.HasValue) ShowTab(tab.Value);
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            var tab = HitTestTab(e.Location);
            _hoveredTab = tab;
            Invalidate();
        }

        private Tab? HitTestTab(Point p)
        {
            var t  = ThemeManager.Current;
            int tx = TabBarPad;
            for (int i = 0; i < TabLabels.Length; i++)
            {
                int w = MeasureTabWidth(t, TabLabels[i]);
                var r = new Rectangle(tx, (TabBarH - 26) / 2, w, 26);
                if (r.Contains(p)) return (Tab)i;
                tx += w + 4;
            }
            return null;
        }

        // ── Autostart ─────────────────────────────────────────────────────────
        private const string RegRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName   = "Auskraft Snap";

        private bool IsAutostartEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegRunKey);
            return key?.GetValue(AppName) != null;
        }

        private void SetAutostart(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegRunKey, writable: true);
            if (key == null) return;
            if (enable) key.SetValue(AppName, Application.ExecutablePath);
            else        key.DeleteValue(AppName, throwOnMissingValue: false);
        }

        // ── Factory helpers ───────────────────────────────────────────────────
        private void AddTabControls(Tab tab, List<Control> controls)
        {
            _tabControls[tab] = controls;
            foreach (var c in controls)
            {
                c.Visible = false;
                _contentPanel.Controls.Add(c);
            }
        }

        private Label SectionLabel(string text, int x, int y) => new()
        {
            Text      = text,
            Location  = new Point(x, y),
            AutoSize  = true,
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(180, 245, 247, 255),
        };

        private TextBox StyledTextBox(int x, int y, int width) => new()
        {
            Bounds      = new Rectangle(x, y, width, 28),
            BackColor   = Color.FromArgb(20, 20, 28),
            ForeColor   = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
        };

        private CheckBox StyledCheckBox(string text, int x, int y) => new()
        {
            Text      = text,
            Location  = new Point(x, y),
            AutoSize  = true,
            BackColor = Color.FromArgb(1, 0, 0, 0), // CheckBox не поддерживает Transparent
            ForeColor = Color.FromArgb(200, 245, 247, 255),
        };

        private Button AccentButton(string text, int x, int y, int w)
        {
            var btn = new Button
            {
                Text      = text,
                Bounds    = new Rectangle(x, y, w, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(20, 80, 60, 180),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand,
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(60, 100, 80, 220);
            return btn;
        }

        private Button SaveButton(int x, int y)
        {
            var btn = AccentButton("Сохранить", x, y, 130);
            btn.Click += (_, _) => SaveAll();
            return btn;
        }

        // ── Rounded region ────────────────────────────────────────────────────
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            using var path = DrawingHelpers.RoundedRect(
                new RectangleF(0, 0, Width, Height), AppTheme.RLg);
            Region = new Region(path);
        }
    }
}