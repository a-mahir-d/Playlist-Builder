using System.Text.Json;

namespace Playlist_Builder.Helpers;

public static class DownloadHelper
{
    private static readonly HttpClient _httpClient = new HttpClient()
    {
        Timeout = TimeSpan.FromMinutes(30)
    };

    public static async Task<(byte[] opusBytes, int durationInSeconds, string error)> DownloadOpusAsync(string pcIp, string youtubeUrl)
    {
        if (string.IsNullOrWhiteSpace(pcIp) || string.IsNullOrWhiteSpace(youtubeUrl))
            return ([], 0, "DownloadHelper: Ip veya youtube url boş olamaz");

        try
        {
            var encodedUrl = Uri.EscapeDataString(youtubeUrl);
            var requestUri = $"http://{pcIp}:8000/download-opus?url={encodedUrl}";
            
            var response = await _httpClient.PostAsync(requestUri, new StringContent(string.Empty));

            if (!response.IsSuccessStatusCode)
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                var obj = JsonSerializer.Deserialize<Dictionary<string, string>>(errorJson);
                var errorMessage = obj != null && obj.TryGetValue("error", out var msg)
                    ? msg
                    : "Bilinmeyen hata";
                return ([], 0, "DownloadHelper: " + errorMessage);
            }
            
            int? duration = null;
            if (response.Headers.TryGetValues("x-duration-seconds", out var values))
            {
                if (int.TryParse(values.FirstOrDefault(), out var d))
                    duration = d;
            }

            var opusBytes = await response.Content.ReadAsByteArrayAsync();
            return (opusBytes, duration ?? 0, "");
        }
        catch (Exception ex)
        {
            return ([], 0, "DownloadHelper: " + ex.Message);
        }
    }
}