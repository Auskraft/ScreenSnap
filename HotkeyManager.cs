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
        private const int ID_FULLSCREEN = 1;
        private const int ID_REGION = 2;

        // Модификаторы
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CTRL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;

        public event Action? FullScreenPressed;
        public event Action? RegionPressed;

        public HotkeyManager()
        {
            CreateHandle(new CreateParams());
        }

        public void Register(AppSettings settings)
        {
            UnregisterHotKey(Handle, ID_FULLSCREEN);
            UnregisterHotKey(Handle, ID_REGION);

            var (mod1, key1) = ParseHotkey(settings.HotkeyFullScreen);
            var (mod2, key2) = ParseHotkey(settings.HotkeyRegion);

            RegisterHotKey(Handle, ID_FULLSCREEN, mod1, key1);
            RegisterHotKey(Handle, ID_REGION, mod2, key2);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                if (m.WParam.ToInt32() == ID_FULLSCREEN) FullScreenPressed?.Invoke();
                if (m.WParam.ToInt32() == ID_REGION) RegionPressed?.Invoke();
            }
            base.WndProc(ref m);
        }

        public static (uint modifiers, uint key) ParseHotkey(string hotkey)
        {
            uint modifiers = 0;
            uint vk = 0;
            var parts = hotkey.Split('+');
            foreach (var part in parts)
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
            UnregisterHotKey(Handle, ID_FULLSCREEN);
            UnregisterHotKey(Handle, ID_REGION);
            DestroyHandle();
        }
    }
}