using SpotifyAPI.Web;
using SpotifyAPI.Web.Http;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyAuthService(HttpClient httpClient)
    {
        private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        public async Task<SpotifyClient> EnsureAuthenticatedAsync(
            SpotifyConfig config,
            SpotifyOAuthHandler oauthHandler,
            CancellationToken cancellationToken = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "A configuração do Spotify não pode ser nula.");

            if (!config.IsAuthenticated)
            {
                Log("Nenhuma configuração encontrada. Abrindo painel no navegador...");
                return await ReconfigureAsync(config, oauthHandler, cancellationToken);
            }

            Log("Credenciais encontradas. Conectando diretamente ao Spotify...");
            return await CreateClientAsync(config.ClientId!, config.ClientSecret!, config.RefreshToken!);
        }

        public async Task<SpotifyClient> ReconfigureAsync(
            SpotifyConfig config,
            SpotifyOAuthHandler oauthHandler,
            CancellationToken cancellationToken = default)
        {
            var (clientId, clientSecret, refreshToken) = await oauthHandler.AuthenticateUserAsync(config, cancellationToken);
            if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                throw new InvalidOperationException("Falha ao salvar as novas configurações do Spotify.");
            }

            config.ClientId = clientId;
            config.ClientSecret = clientSecret;
            config.RefreshToken = refreshToken;

            return await CreateClientAsync(config.ClientId, config.ClientSecret, config.RefreshToken);
        }

        public async Task<string> ExchangeCodeForRefreshTokenAsync(
            string clientId,
            string clientSecret,
            string code,
            string redirectUri)
        {
            var config = SpotifyClientConfig.CreateDefault().WithHTTPClient(new NetHttpClient(_httpClient));
            var oauthClient = new OAuthClient(config);

            var response = await oauthClient.RequestToken(
                new AuthorizationCodeTokenRequest(clientId, clientSecret, code, new Uri(redirectUri))
            );

            return response.RefreshToken;
        }

        public async Task<SpotifyClient> CreateClientAsync(string clientId, string clientSecret, string refreshToken)
        {
            var config = SpotifyClientConfig
                .CreateDefault()
                .WithHTTPClient(new NetHttpClient(_httpClient))
                .WithAuthenticator(new AuthorizationCodeAuthenticator(clientId, clientSecret, new AuthorizationCodeTokenResponse
                {
                    RefreshToken = refreshToken
                }));

            var client = new SpotifyClient(config);

            var me = await client.UserProfile.Current();
            Log($"Conectado com sucesso como: {me.DisplayName} ({me.Id})");
            return client;
        }

        private static void Log(string message)
        {
            Console.WriteLine($"[SpotifyAuthService] {message}");
        }
    }
}