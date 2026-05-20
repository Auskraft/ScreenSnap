using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

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

            // Открываем браузер
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

            // Показываем окно для вставки токена
            var form = new TokenInputForm();
            if (form.ShowDialog() == DialogResult.OK)
            {
                accessToken = form.Token;
                // Сохраняем токен в настройки
                var settings = AppSettings.Load();
                settings.YandexToken = accessToken;
                settings.Save();
            }
        }

        public async Task<bool> UploadAsync(string filePath)
        {
            if (!IsAuthorized) return false;

            try
            {
                var fileName = Path.GetFileName(filePath);
                var remotePath = $"/ScreenSnap/{fileName}";

                // Получаем URL для загрузки
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"https://cloud-api.yandex.net/v1/disk/resources/upload?path={Uri.EscapeDataString(remotePath)}&overwrite=true");
                request.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);

                var response = await http.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var uploadUrl = doc.RootElement.GetProperty("href").GetString();

                // Загружаем файл
                using var fileStream = File.OpenRead(filePath);
                var uploadRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
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
    }
}