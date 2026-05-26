using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenSnap
{
    public class YandexDiskService
    {
        private static readonly HttpClient http = new HttpClient();
        private string? accessToken;

        public bool IsAuthorized => !string.IsNullOrEmpty(accessToken);

        public void SetToken(string token)
        {
            accessToken = token;
        }

        public async Task AuthorizeAsync()
        {
            var clientId = Environment.GetEnvironmentVariable("YANDEX_CLIENT_ID");
            var url = $"https://oauth.yandex.ru/authorize?response_type=token&client_id={clientId}";

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

            var form = new TokenInputForm();
            if (form.ShowDialog() == DialogResult.OK)
            {
                accessToken = form.Token;
                var settings = AppSettings.Load();
                settings.YandexToken = accessToken;
                settings.Save();
            }
        }

        /// <summary>
        /// Загружает файл на Яндекс.Диск в /AuskraftSnap/{fileName}.
        /// Возвращает true при успехе.
        /// </summary>
        public async Task<bool> UploadAsync(string filePath)
        {
            if (!IsAuthorized) return false;

            try
            {
                var fileName   = System.IO.Path.GetFileName(filePath);
                var remotePath = $"/AuskraftSnap/{fileName}";

                // Получаем URL для загрузки
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"https://cloud-api.yandex.net/v1/disk/resources/upload?path={Uri.EscapeDataString(remotePath)}&overwrite=true");
                request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);

                var response = await http.SendAsync(request);
                var json     = await response.Content.ReadAsStringAsync();
                var doc      = JsonDocument.Parse(json);
                var uploadUrl = doc.RootElement.GetProperty("href").GetString();

                // Загружаем файл
                using var fileStream    = System.IO.File.OpenRead(filePath);
                var uploadRequest       = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
                {
                    Content = new StreamContent(fileStream)
                };
                uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);

                var uploadResponse = await http.SendAsync(uploadRequest);
                return uploadResponse.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Публикует файл и возвращает публичную ссылку.
        /// fileName — только имя файла (без пути), папка /AuskraftSnap/.
        /// Возвращает null при ошибке или если не авторизован.
        /// </summary>
        public async Task<string?> GetPublicLinkAsync(string fileName)
        {
            if (!IsAuthorized) return null;

            try
            {
                var remotePath = $"/AuskraftSnap/{fileName}";

                // Публикуем ресурс
                var pubRequest = new HttpRequestMessage(HttpMethod.Put,
                    $"https://cloud-api.yandex.net/v1/disk/resources/publish?path={Uri.EscapeDataString(remotePath)}");
                pubRequest.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
                await http.SendAsync(pubRequest);

                // Получаем метаданные с public_url
                var metaRequest = new HttpRequestMessage(HttpMethod.Get,
                    $"https://cloud-api.yandex.net/v1/disk/resources?path={Uri.EscapeDataString(remotePath)}&fields=public_url");
                metaRequest.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);

                var metaResponse = await http.SendAsync(metaRequest);
                var metaJson     = await metaResponse.Content.ReadAsStringAsync();
                var metaDoc      = JsonDocument.Parse(metaJson);

                if (metaDoc.RootElement.TryGetProperty("public_url", out var urlProp))
                    return urlProp.GetString();

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}