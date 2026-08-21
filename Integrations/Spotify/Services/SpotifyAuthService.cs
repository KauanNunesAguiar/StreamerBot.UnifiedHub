using SpotifyAPI.Web;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyAuthService
    {
        public static async Task<SpotifyClient> EnsureAuthenticatedAsync(
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
            return await CreateClientAsync(config);
        }

        public static async Task<SpotifyClient> ReconfigureAsync(
            SpotifyConfig config,
            SpotifyOAuthHandler oauthHandler,
            CancellationToken cancellationToken = default)
        {
            var result = await oauthHandler.AuthenticateUserAsync(config, cancellationToken);

            if (string.IsNullOrEmpty(result.RefreshToken) || string.IsNullOrEmpty(result.ClientId) || string.IsNullOrEmpty(result.ClientSecret))
                throw new InvalidOperationException("Falha ao salvar as novas configurações do Spotify.");

            config.ClientId = result.ClientId;
            config.ClientSecret = result.ClientSecret;
            config.RefreshToken = result.RefreshToken;
            config.AccessToken = string.Empty;
            config.TokenExpiration = DateTime.MinValue;

            if (result.ExtraSettings.TryGetValue("PlaylistId", out string? playlistId) && !string.IsNullOrWhiteSpace(playlistId))
                config.PlaylistId = playlistId;

            return await CreateClientAsync(config);
        }

        public static async Task<string> ExchangeCodeForRefreshTokenAsync(
            string clientId,
            string clientSecret,
            string code,
            string redirectUri)
        {
            var response = await new OAuthClient().RequestToken(
                new AuthorizationCodeTokenRequest(clientId, clientSecret, code, new Uri(redirectUri))
            );

            return response.RefreshToken;
        }

        public static async Task<SpotifyClient> CreateClientAsync(SpotifyConfig config)
        {
            AuthorizationCodeTokenResponse tokenResponse;

            bool hasValidAccessToken = !string.IsNullOrEmpty(config.AccessToken)
                && config.TokenExpiration > DateTime.UtcNow.AddMinutes(1);

            if (hasValidAccessToken)
            {
                Log("Reutilizando Access Token salvo (ainda válido)...");
                tokenResponse = new AuthorizationCodeTokenResponse
                {
                    AccessToken = config.AccessToken,
                    RefreshToken = config.RefreshToken,
                    ExpiresIn = (int)(config.TokenExpiration - DateTime.UtcNow).TotalSeconds,
                    CreatedAt = DateTime.UtcNow
                };
            }
            else
            {
                Log("Access Token ausente ou expirado. Solicitando novo ao Spotify...");
                var oauthClient = new OAuthClient();
                var refreshResponse = await oauthClient.RequestToken(
                    new AuthorizationCodeRefreshRequest(config.ClientId, config.ClientSecret, config.RefreshToken)
                );

                // AuthorizationCodeRefreshResponse não traz RefreshToken novo (Spotify reaproveita o antigo),
                // então montamos o TokenResponse combinando os dois.
                tokenResponse = new AuthorizationCodeTokenResponse
                {
                    AccessToken = refreshResponse.AccessToken,
                    RefreshToken = config.RefreshToken,
                    ExpiresIn = refreshResponse.ExpiresIn,
                    CreatedAt = refreshResponse.CreatedAt,
                    Scope = refreshResponse.Scope,
                    TokenType = refreshResponse.TokenType
                };

                config.AccessToken = tokenResponse.AccessToken;
                config.TokenExpiration = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
            }

            var spotifyConfig = SpotifyClientConfig
                .CreateDefault()
                .WithAuthenticator(new AuthorizationCodeAuthenticator(config.ClientId, config.ClientSecret, tokenResponse));

            var client = new SpotifyClient(spotifyConfig);

            var me = await client.UserProfile.Current();
            Log($"Conectado com sucesso como: {me.DisplayName} ({me.Id})");

            return client;
        }

        public static async Task<SpotifyClient> CreateClientFromRefreshTokenAsync(string clientId, string clientSecret, string refreshToken)
        {
            var oauthClient = new OAuthClient();
            var refreshResponse = await oauthClient.RequestToken(
                new AuthorizationCodeRefreshRequest(clientId, clientSecret, refreshToken));

            var tokenResponse = new AuthorizationCodeTokenResponse
            {
                AccessToken = refreshResponse.AccessToken,
                RefreshToken = refreshToken,
                ExpiresIn = refreshResponse.ExpiresIn,
                CreatedAt = refreshResponse.CreatedAt,
                Scope = refreshResponse.Scope,
                TokenType = refreshResponse.TokenType
            };

            var spotifyConfig = SpotifyClientConfig
                .CreateDefault()
                .WithAuthenticator(new AuthorizationCodeAuthenticator(clientId, clientSecret, tokenResponse));

            return new SpotifyClient(spotifyConfig);
        }

        private static void Log(string message)
        {
            Console.WriteLine($"[SpotifyAuthService] {message}");
        }
    }
}