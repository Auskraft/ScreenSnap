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

        private const int WM_HOTKEY = 0x0312;

        // ── ID хоткеев ────────────────────────────────────────────────────────
        private const int ID_FULLSCREEN      = 1;
        private const int ID_REGION          = 2;
        private const int ID_QUICK_SHARE     = 3;  // Ctrl+Shift+S — Фаза 3
        private const int ID_DOC_MODE        = 4;  // Ctrl+Shift+D — Фаза 3
        private const int ID_PRIVACY_MODE    = 5;  // Ctrl+Shift+P — Фаза 3
        private const int ID_SAVE_PIN        = 6;  // Ctrl+Shift+T — Фаза 3
        private const int ID_CMD_PALETTE     = 7;  // Ctrl+K       — Фаза 3

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

        public HotkeyManager()
        {
            CreateHandle(new CreateParams());
        }

        public void Register(AppSettings settings)
        {
            // Снимаем все предыдущие регистрации
            for (int id = ID_FULLSCREEN; id <= ID_CMD_PALETTE; id++)
                UnregisterHotKey(Handle, id);

            TryRegister(ID_FULLSCREEN,   settings.HotkeyFullScreen);
            TryRegister(ID_REGION,       settings.HotkeyRegion);
            TryRegister(ID_QUICK_SHARE,  settings.HotkeyQuickShare);
            TryRegister(ID_CMD_PALETTE,  settings.HotkeyCommandPalette);

            // Хоткеи пресетов — пока хардкод (в Фазе 4 вынесем в AppSettings)
            TryRegister(ID_DOC_MODE,     "Ctrl+Shift+D");
            TryRegister(ID_PRIVACY_MODE, "Ctrl+Shift+P");
            TryRegister(ID_SAVE_PIN,     "Ctrl+Shift+T");
        }

        private void TryRegister(int id, string hotkey)
        {
            var (mod, key) = ParseHotkey(hotkey);
            if (key == 0) return; // невалидный хоткей — пропускаем
            RegisterHotKey(Handle, id, mod, key);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                switch (m.WParam.ToInt32())
                {
                    case ID_FULLSCREEN:   FullScreenPressed?.Invoke();      break;
                    case ID_REGION:       RegionPressed?.Invoke();           break;
                    case ID_QUICK_SHARE:  QuickSharePressed?.Invoke();       break;
                    case ID_DOC_MODE:     DocModePressed?.Invoke();          break;
                    case ID_PRIVACY_MODE: PrivacyModePressed?.Invoke();      break;
                    case ID_SAVE_PIN:     SavePinPressed?.Invoke();          break;
                    case ID_CMD_PALETTE:  CommandPalettePressed?.Invoke();   break;
                }
            }
            base.WndProc(ref m);
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
            for (int id = ID_FULLSCREEN; id <= ID_CMD_PALETTE; id++)
                UnregisterHotKey(Handle, id);
            DestroyHandle();
        }
    }
}