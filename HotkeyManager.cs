using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScreenSnap
{
    public class HotkeyManager : NativeWindow, IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern int PrintWindow(IntPtr hwnd, IntPtr hdcBlt, int nFlags);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        private const int WM_HOTKEY = 0x0312;

        // ── ID хоткеев ────────────────────────────────────────────────────────
        private const int ID_FULLSCREEN      = 1;
        private const int ID_REGION          = 2;
        private const int ID_QUICK_SHARE     = 3;  // Ctrl+Shift+S
        private const int ID_DOC_MODE        = 4;  // Ctrl+Shift+D
        private const int ID_PRIVACY_MODE    = 5;  // Ctrl+Shift+P
        private const int ID_SAVE_PIN        = 6;  // Ctrl+Shift+T
        private const int ID_CMD_PALETTE     = 7;  // Ctrl+K
        private const int ID_ACTIVE_WINDOW   = 8;  // Ctrl+Shift+W — Фаза 4
        private const int ID_REPEAT_LAST     = 9;  // Ctrl+Shift+R — Фаза 4

        // Модификаторы
        private const uint MOD_ALT   = 0x0001;
        private const uint MOD_CTRL  = 0x0002;
        private const uint MOD_SHIFT = 0x0004;

        // ── События ───────────────────────────────────────────────────────────
        public event Action? FullScreenPressed;
        public event Action? RegionPressed;
        public event Action? QuickSharePressed;
        public event Action? DocModePressed;
        public event Action? PrivacyModePressed;
        public event Action? SavePinPressed;
        public event Action? CommandPalettePressed;
        public event Action? ActiveWindowPressed;
        public event Action? RepeatLastPressed;

        public HotkeyManager()
        {
            CreateHandle(new CreateParams());
        }

        public void Register(AppSettings settings)
        {
            // Снимаем все предыдущие регистрации
            for (int id = ID_FULLSCREEN; id <= ID_REPEAT_LAST; id++)
                UnregisterHotKey(Handle, id);

            TryRegister(ID_FULLSCREEN,     settings.HotkeyFullScreen);
            TryRegister(ID_REGION,         settings.HotkeyRegion);
            TryRegister(ID_QUICK_SHARE,    settings.HotkeyQuickShare);
            TryRegister(ID_CMD_PALETTE,    settings.HotkeyCommandPalette);

            // Хоткеи пресетов — хардкод (вынести в AppSettings в Фазе 5)
            TryRegister(ID_DOC_MODE,       "Ctrl+Shift+D");
            TryRegister(ID_PRIVACY_MODE,   "Ctrl+Shift+P");
            TryRegister(ID_SAVE_PIN,       "Ctrl+Shift+T");
            TryRegister(ID_ACTIVE_WINDOW,  "Ctrl+Shift+W");
            TryRegister(ID_REPEAT_LAST,    "Ctrl+Shift+R");
        }

        private void TryRegister(int id, string hotkey)
        {
            var (mod, key) = ParseHotkey(hotkey);
            if (key == 0) return;
            RegisterHotKey(Handle, id, mod, key);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                switch (m.WParam.ToInt32())
                {
                    case ID_FULLSCREEN:    FullScreenPressed?.Invoke();     break;
                    case ID_REGION:        RegionPressed?.Invoke();          break;
                    case ID_QUICK_SHARE:   QuickSharePressed?.Invoke();      break;
                    case ID_DOC_MODE:      DocModePressed?.Invoke();         break;
                    case ID_PRIVACY_MODE:  PrivacyModePressed?.Invoke();     break;
                    case ID_SAVE_PIN:      SavePinPressed?.Invoke();         break;
                    case ID_CMD_PALETTE:   CommandPalettePressed?.Invoke();  break;
                    case ID_ACTIVE_WINDOW: ActiveWindowPressed?.Invoke();    break;
                    case ID_REPEAT_LAST:   RepeatLastPressed?.Invoke();      break;
                }
            }
            base.WndProc(ref m);
        }

        // ── Захват активного окна ─────────────────────────────────────────────
        /// <summary>
        /// Делает скриншот текущего активного окна (foreground window).
        /// Возвращает Bitmap или null если окно не найдено.
        /// </summary>
        public static System.Drawing.Bitmap? CaptureActiveWindow()
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;

            if (!GetWindowRect(hwnd, out var rect)) return null;

            int w = rect.Right  - rect.Left;
            int h = rect.Bottom - rect.Top;
            if (w <= 0 || h <= 0) return null;

            var bmp = new System.Drawing.Bitmap(w, h,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using var g = System.Drawing.Graphics.FromImage(bmp);
            var hdc = g.GetHdc();
            PrintWindow(hwnd, hdc, 0x2); // PW_RENDERFULLCONTENT
            g.ReleaseHdc(hdc);

            return bmp;
        }

        public static (uint modifiers, uint key) ParseHotkey(string hotkey)
        {
            uint modifiers = 0;
            uint vk        = 0;
            foreach (var part in hotkey.Split('+'))
            {
                switch (part.Trim().ToLower())
                {
                    case "ctrl":  modifiers |= MOD_CTRL;  break;
                    case "shift": modifiers |= MOD_SHIFT; break;
                    case "alt":   modifiers |= MOD_ALT;   break;
                    default:
                        if (Enum.TryParse<Keys>(part.Trim(), true, out var k))
                            vk = (uint)k;
                        break;
                }
            }
            return (modifiers, vk);
        }

        public void Dispose()
        {
            for (int id = ID_FULLSCREEN; id <= ID_REPEAT_LAST; id++)
                UnregisterHotKey(Handle, id);
            DestroyHandle();
        }
    }
}