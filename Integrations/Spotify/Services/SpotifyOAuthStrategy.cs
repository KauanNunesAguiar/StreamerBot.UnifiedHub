using System.Reflection;
using StreamerBot.UnifiedHub.Core.Abstractions;
using StreamerBot.UnifiedHub.Core.Services;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyOAuthStrategy(SpotifyService spotifyService) : IOAuthFlowStrategy
    {
        private const string LoginHtmlResourceName = "StreamerBot.UnifiedHub.Integrations.Spotify.Assets.spotify-login.html";

        private readonly SpotifyService _spotifyService = spotifyService;

        public string InvalidCredentialsMessage =>
            "O Client ID informado é inválido ou não existe no Spotify Developer Dashboard.";

        public string BuildAuthorizationUrl(string clientId, string redirectUri)
        {
            string scopes = Uri.EscapeDataString(
                "user-read-currently-playing " +
                "user-read-playback-state " +
                "user-modify-playback-state " +
                "user-read-recently-played " +
                "user-library-modify " +
                "user-library-read " +
                "playlist-read-private " +
                "playlist-read-collaborative " +
                "playlist-modify-public " +
                "playlist-modify-private"
            );

            return $"https://accounts.spotify.com/authorize?response_type=code&client_id={clientId}&scope={scopes}&redirect_uri={Uri.EscapeDataString(redirectUri)}";
        }

        public async Task<string> ExchangeCodeForRefreshTokenAsync(string clientId, string clientSecret, string code, string redirectUri)
        {
            return await _spotifyService.ExchangeCodeForRefreshTokenAsync(clientId, clientSecret, code, redirectUri);
        }

        public string BuildExchangeErrorMessage(Exception ex) =>
            $"Falha ao validar credenciais (Client Secret pode estar incorreto): {ex.Message}";

        public string RenderFormHtml(string clientId, string clientSecret, string? erro)
        {
            string template = EmbeddedTemplateRenderer.Load(Assembly.GetExecutingAssembly(), LoginHtmlResourceName);

            string divErro = string.IsNullOrEmpty(erro)
                ? string.Empty
                : $"<div class=\"error\">{erro}</div>";

            return EmbeddedTemplateRenderer.Render(template, new Dictionary<string, string>
            {
                ["{{ERROR_SECTION}}"] = divErro,
                ["{{CLIENT_ID}}"] = clientId,
                ["{{CLIENT_SECRET}}"] = clientSecret
            });
        }

        public async Task<bool> ValidateCredentialsAsync(string clientId, string clientSecret)
        {
            try
            {
                using var httpClient = new HttpClient();

                var authHeader = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);

                var content = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                ]);

                var response = await httpClient.PostAsync("https://accounts.spotify.com/api/token", content);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (responseBody.Contains("invalid_client"))
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return true;
            }
        }
    }
}