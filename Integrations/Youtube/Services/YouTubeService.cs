using System.Net.Http.Json;

namespace StreamerBot.UnifiedHub.Integrations.Youtube.Services
{
    public class YouTubeService(HttpClient httpClient)
    {
        private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        public async Task<string?> GetVideoTitleAsync(string youtubeUrl, CancellationToken cancellationToken = default)
        {
            try
            {
                string oembedUrl = $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(youtubeUrl)}&format=json";
                var response = await _httpClient.GetFromJsonAsync<YouTubeOEmbedResponse>(oembedUrl, cancellationToken);
                return response?.Title;
            }
            catch
            {
                return null;
            }
        }

        private record YouTubeOEmbedResponse(string Title);
    }
}