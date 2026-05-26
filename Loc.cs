using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows.Forms;

namespace ScreenSnap
{
    // ═══════════════════════════════════════════════════════════════════════
    //  Loc — локализация
    //
    //  Использование:
    //    Loc.Get("share_done")              // → "Загружено!" (ru) / "Uploaded!" (en)
    //    Loc.Get("capture_saved", "img.png") // → "Сохранено: img.png"
    //    Loc.Culture = new CultureInfo("en") // переключить язык
    // ═══════════════════════════════════════════════════════════════════════
    public static class Loc
    {
        private static ResourceManager? _rm;
        private static CultureInfo      _culture = CultureInfo.CurrentUICulture;

        // Кэш для быстрого доступа
        private static readonly Dictionary<string, string> _cache = new();

        public static CultureInfo Culture
        {
            get => _culture;
            set { _culture = value; _cache.Clear(); }
        }

        private static ResourceManager Rm
        {
            get
            {
                if (_rm == null)
                {
                    var asm = Assembly.GetExecutingAssembly();
                    _rm = new ResourceManager("AuskraftSnap.Resources", asm);
                }
                return _rm;
            }
        }

        /// <summary>
        /// Получить строку по ключу. Fallback: en → ключ.
        /// </summary>
        public static string Get(string key)
        {
            if (_cache.TryGetValue(key, out var cached)) return cached;

            string? value = null;
            try
            {
                value = Rm.GetString(key, _culture);
            }
            catch { /* ignore */ }

            // Fallback на invariant (английский)
            if (string.IsNullOrEmpty(value))
            {
                try { value = Rm.GetString(key, CultureInfo.InvariantCulture); }
                catch { /* ignore */ }
            }

            var result = string.IsNullOrEmpty(value) ? key : value;
            _cache[key] = result;
            return result;
        }

        /// <summary>
        /// Получить строку с форматированием: Loc.Get("capture_saved", "img.png")
        /// </summary>
        public static string Get(string key, params object[] args)
        {
            var template = Get(key);
            try { return string.Format(template, args); }
            catch { return template; }
        }

        /// <summary>
        /// Определить язык системы и выставить культуру.
        /// Вызывать один раз при старте в Program.cs.
        /// </summary>
        public static void AutoDetect()
        {
            var lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            _culture = lang == "ru"
                ? new CultureInfo("ru")
                : CultureInfo.InvariantCulture;
            _cache.Clear();
        }

        // ── Shorthand-свойства для часто используемых строк ─────────────────
        // Share overlay
        public static string ShareDone     => Get("share_done");
        public static string ShareOpen     => Get("share_open");
        public static string ShareMd       => Get("share_md");
        public static string ShareHtml     => Get("share_html");
        public static string ShareQr       => Get("share_qr");
        public static string ShareDelete   => Get("share_delete");
        public static string ShareDismiss  => Get("share_dismiss");
        public static string ShareCopyLink => Get("share_copy_link");
        public static string ShareCopied   => Get("share_copied");

        // Command palette
        public static string CmdCapture   => Get("cmd_capture");
        public static string CmdWorkflows => Get("cmd_workflows");
        public static string CmdLibrary   => Get("cmd_library");
        public static string CmdActions   => Get("cmd_actions");
        public static string CmdJump      => Get("cmd_jump");

        // Toast steps
        public static string StepCapture  => Get("step_capture");
        public static string StepCompress => Get("step_compress");
        public static string StepUpload   => Get("step_upload");
        public static string StepCopyLink => Get("step_copy_link");
        public static string StepBlur     => Get("step_blur");
        public static string StepSave     => Get("step_save");
        public static string StepPin      => Get("step_pin");

        // Editor
        public static string EditorSave      => Get("editor_save");
        public static string EditorCopy      => Get("editor_copy");
        public static string EditorUndo      => Get("editor_undo");
        public static string EditorLayers    => Get("editor_layers");
        public static string EditorNoObjects => Get("editor_no_objects");
        public static string EditorUpload    => Get("editor_upload");
        public static string EditorUploaded  => Get("editor_uploaded");
        public static string EditorNoImage   => Get("editor_no_image");
        public static string EditorTextPrompt => Get("editor_text_prompt");
    }
}
