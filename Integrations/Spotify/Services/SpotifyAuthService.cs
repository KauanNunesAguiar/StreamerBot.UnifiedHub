using SpotifyAPI.Web;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyAuthService
    {
        /// <summary>
        /// Garante a conexão. Se o usuário já tiver o RefreshToken, conecta diretamente.
        /// Caso contrário (primeiro acesso), abre o painel no navegador.
        /// </summary>
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
            return await CreateClientAsync(config);
        }

        /// <summary>
        /// Força a abertura da página no navegador para alterar Client ID / Secret, 
        /// mantendo os dados anteriores pré-preenchidos.
        /// </summary>
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
            // Zera para forçar um refresh completo já que é uma nova credencial
            config.AccessToken = string.Empty;
            config.TokenExpiration = DateTime.MinValue;

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

        /// <summary>
        /// Cria o SpotifyClient reaproveitando o AccessToken salvo se ainda for válido,
        /// ou renovando via RefreshToken apenas quando necessário.
        /// </summary>
        public async Task<SpotifyClient> CreateClientAsync(SpotifyConfig config)
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

        private static void Log(string message)
        {
            Console.WriteLine($"[SpotifyAuthService] {message}");
        }
    }
}