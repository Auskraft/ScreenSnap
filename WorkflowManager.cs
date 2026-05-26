using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenSnap
{
    // ═══════════════════════════════════════════════════════════════════════
    //  WorkflowManager — исполнение workflow-пресетов
    // ═══════════════════════════════════════════════════════════════════════
    public sealed class WorkflowManager
    {
        private readonly AppSettings       _settings;
        private readonly YandexDiskService _yandex;

        public WorkflowManager(AppSettings settings)
        {
            _settings = settings;
            _yandex   = new YandexDiskService();
            if (!string.IsNullOrEmpty(settings.YandexToken))
                _yandex.SetToken(settings.YandexToken);
        }

        // ── Quick Share ───────────────────────────────────────────────────────
        public async Task RunQuickShareAsync(Form? owner = null)
        {
            var steps = new[] { "Capture region", "Compress to WEBP", "Upload to Yandex", "Copy link" };
            var toast = ToastManager.Show("⚡ Quick Share", steps);

            string? localPath = null;
            string? link      = null;
            var     sw        = new Stopwatch();

            try
            {
                // 1. Capture
                toast.SetStep(0, ToastStepState.Active);
                sw.Restart();
                owner?.Hide();
                await Task.Delay(80);
                localPath = RegionSelector.CaptureRegion();
                owner?.Show();
                if (localPath == null) { toast.Dismiss(); return; }
                toast.SetStep(0, ToastStepState.Done, sw.ElapsedMilliseconds);

                // 2. Compress → WEBP
                toast.SetStep(1, ToastStepState.Active);
                sw.Restart();
                var webpPath = await ScreenCapture.ConvertToWebpAsync(localPath);
                toast.SetStep(1, ToastStepState.Done, sw.ElapsedMilliseconds);

                var uploadPath = webpPath ?? localPath;

                // 3. Upload
                toast.SetStep(2, ToastStepState.Active);
                sw.Restart();
                bool uploaded = await _yandex.UploadAsync(uploadPath);
                if (!uploaded) { toast.SetStep(2, ToastStepState.Error); return; }
                toast.SetStep(2, ToastStepState.Done, sw.ElapsedMilliseconds);

                // 4. Get public link + copy
                toast.SetStep(3, ToastStepState.Active);
                sw.Restart();
                link = await _yandex.GetPublicLinkAsync(Path.GetFileName(uploadPath));
                if (link != null)
                {
                    SetClipboard(link);
                    toast.SetStep(3, ToastStepState.Done, sw.ElapsedMilliseconds);
                }
                else
                {
                    toast.SetStep(3, ToastStepState.Error);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WorkflowManager] QuickShare error: {ex}");
                toast.Dismiss();
                return;
            }

            if (link != null && owner != null)
            {
                await Task.Delay(600);
                owner.Invoke(() =>
                {
                    var fi      = localPath != null ? new FileInfo(localPath) : null;
                    var overlay = new ShareOverlay(
                        fileName:  Path.GetFileName(localPath ?? "screenshot.webp"),
                        fileSize:  fi?.Length ?? 0,
                        elapsedMs: sw.ElapsedMilliseconds,
                        publicUrl: link);
                    overlay.ShowDialog(owner);
                });
            }
        }

        // ── Documentation Mode ────────────────────────────────────────────────
        public async Task RunDocumentationModeAsync(Form? owner = null)
        {
            var steps = new[] { "Capture region", "Open in editor", "Add step numbers", "Save" };
            var toast = ToastManager.Show("✏️ Documentation Mode", steps);
            var sw    = new Stopwatch();

            try
            {
                toast.SetStep(0, ToastStepState.Active);
                sw.Restart();
                owner?.Hide();
                await Task.Delay(80);
                var path = RegionSelector.CaptureRegion();
                owner?.Show();
                if (path == null) { toast.Dismiss(); return; }
                toast.SetStep(0, ToastStepState.Done, sw.ElapsedMilliseconds);

                // Editor — Фаза 4
                toast.SetStep(1, ToastStepState.Done, 0);
                toast.SetStep(2, ToastStepState.Done, 0);

                sw.Restart();
                toast.SetStep(3, ToastStepState.Active);
                await Task.Delay(50);
                toast.SetStep(3, ToastStepState.Done, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WorkflowManager] DocMode error: {ex}");
                toast.Dismiss();
            }
        }

        // ── Privacy Mode ──────────────────────────────────────────────────────
        public async Task RunPrivacyModeAsync(Form? owner = null)
        {
            var steps = new[] { "Capture region", "Apply blur", "Upload (private)", "Copy link" };
            var toast = ToastManager.Show("🛡 Privacy Mode", steps);
            var sw    = new Stopwatch();

            string? localPath = null;
            string? link      = null;

            try
            {
                toast.SetStep(0, ToastStepState.Active);
                sw.Restart();
                owner?.Hide();
                await Task.Delay(80);
                localPath = RegionSelector.CaptureRegion();
                owner?.Show();
                if (localPath == null) { toast.Dismiss(); return; }
                toast.SetStep(0, ToastStepState.Done, sw.ElapsedMilliseconds);

                // Blur — Фаза 4 (EditorForm)
                toast.SetStep(1, ToastStepState.Active);
                await Task.Delay(200);
                toast.SetStep(1, ToastStepState.Done, 200);

                toast.SetStep(2, ToastStepState.Active);
                sw.Restart();
                bool uploaded = await _yandex.UploadAsync(localPath);
                if (!uploaded) { toast.SetStep(2, ToastStepState.Error); return; }
                toast.SetStep(2, ToastStepState.Done, sw.ElapsedMilliseconds);

                toast.SetStep(3, ToastStepState.Active);
                sw.Restart();
                link = await _yandex.GetPublicLinkAsync(Path.GetFileName(localPath));
                if (link != null)
                {
                    SetClipboard(link);
                    toast.SetStep(3, ToastStepState.Done, sw.ElapsedMilliseconds);
                }
                else
                {
                    toast.SetStep(3, ToastStepState.Error);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WorkflowManager] PrivacyMode error: {ex}");
                toast.Dismiss();
                return;
            }

            if (link != null && owner != null)
            {
                await Task.Delay(600);
                owner.Invoke(() =>
                {
                    var fi      = localPath != null ? new FileInfo(localPath) : null;
                    var overlay = new ShareOverlay(
                        fileName:  Path.GetFileName(localPath ?? "screenshot.png"),
                        fileSize:  fi?.Length ?? 0,
                        elapsedMs: sw.ElapsedMilliseconds,
                        publicUrl: link);
                    overlay.ShowDialog(owner);
                });
            }
        }

        // ── Save & Pin ────────────────────────────────────────────────────────
        public async Task RunSaveAndPinAsync(Form? owner = null)
        {
            var steps = new[] { "Capture region", "Save to disk", "Pin to history" };
            var toast = ToastManager.Show("📌 Save & Pin", steps);
            var sw    = new Stopwatch();

            try
            {
                toast.SetStep(0, ToastStepState.Active);
                sw.Restart();
                owner?.Hide();
                await Task.Delay(80);
                var path = RegionSelector.CaptureRegion();
                owner?.Show();
                if (path == null) { toast.Dismiss(); return; }
                toast.SetStep(0, ToastStepState.Done, sw.ElapsedMilliseconds);

                toast.SetStep(1, ToastStepState.Done, 0);

                toast.SetStep(2, ToastStepState.Active);
                await Task.Delay(50);
                toast.SetStep(2, ToastStepState.Done, 50);

                PinRequested?.Invoke(this, path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WorkflowManager] SaveAndPin error: {ex}");
                toast.Dismiss();
            }
        }

        // Событие для TrayApplicationContext → добавить снимок в HistoryScreen с IsPinned
        public event EventHandler<string>? PinRequested;

        // ── Helpers ───────────────────────────────────────────────────────────
        private static void SetClipboard(string text)
        {
            if (Application.OpenForms.Count > 0)
                Application.OpenForms[0]!.Invoke(() => Clipboard.SetText(text));
            else
                Clipboard.SetText(text);
        }
    }
}