using System.Reflection;
using StreamerBot.UnifiedHub.Core.Abstractions;
using StreamerBot.UnifiedHub.Core.Services;
namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyOAuthStrategy : IOAuthFlowStrategy
    {
        private const string LoginHtmlResourceName = "StreamerBot.UnifiedHub.Integrations.Spotify.Assets.spotify-login.html";

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
            return await SpotifyAuthService.ExchangeCodeForRefreshTokenAsync(clientId, clientSecret, code, redirectUri);
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
                string authHeader = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
                {
                    Content = new FormUrlEncodedContent(
                    [
                        new KeyValuePair<string, string>("grant_type", "client_credentials")
                    ])
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);

                var response = await SharedHttpClient.Instance.SendAsync(request);
                string responseBody = await response.Content.ReadAsStringAsync();

                return !responseBody.Contains("invalid_client");
            }
            catch
            {
                return true;
            }
        }
    }
}