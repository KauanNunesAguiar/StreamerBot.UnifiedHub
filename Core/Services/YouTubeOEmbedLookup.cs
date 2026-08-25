using System.Net.Http.Json;

namespace StreamerBot.UnifiedHub.Core.Services
{
    /// <summary>
    /// Consulta o endpoint público oEmbed do YouTube para resolver o título de um vídeo
    /// a partir da URL. Não é a integração YouTube (chat/live) - é apenas um helper de
    /// lookup usado por outras integrações (ex: Spotify) para resolver links do YouTube.
    /// </summary>
    public class YouTubeOEmbedLookup(HttpClient httpClient)
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