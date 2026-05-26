using System;
using System.Windows.Forms;

namespace ScreenSnap
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // ── 1. Load brand fonts before any window paints ──────────────
            FontLoader.Initialize();

            // ── 2. Launch tray context (manages hotkeys + tray icon)
            //        TrayApplicationContext opens MainWindow when needed.
            Application.Run(new TrayApplicationContext());
        }
    }
}