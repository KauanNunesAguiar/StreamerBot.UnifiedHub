using SpotifyAPI.Web;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyAuthService(SpotifyOAuthHandler oauthHandler)
    {
        private readonly SpotifyOAuthHandler _oauthHandler = oauthHandler ?? throw new ArgumentNullException(nameof(oauthHandler));
        private SpotifyClient? _spotifyClient;

        public async Task<SpotifyClient?> GetClientAsync()
        {
            if (_spotifyClient != null)
                return _spotifyClient;

            return await CreateClientAsync();
        }

        public async Task<SpotifyClient?> CreateClientAsync()
        {
            var tokenResponse = await _oauthHandler.GetOrRefreshTokenAsync();
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                return null;
            }

            var config = SpotifyClientConfig
                .CreateDefault()
                .WithAuthenticator(new AuthorizationCodeTokenAuthenticator(
                    _oauthHandler.ClientId,
                    _oauthHandler.ClientSecret,
                    tokenResponse));

            _spotifyClient = new SpotifyClient(config);
            return _spotifyClient;
        }

        public void ClearClient()
        {
            _spotifyClient = null;
        }

        public async Task<SpotifyClient> EnsureAuthenticatedAsync(
            SpotifyConfig config,
            SpotifyOAuthHandler oauthHandler,
            CancellationToken cancellationToken = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "A configuração do Spotify não pode ser nula.");

            // Se ainda não tem autenticação salva
            if (!config.IsAuthenticated)
            {
                Log("Nenhuma configuração encontrada. Abrindo painel no navegador...");
                return await ReconfigureAsync(config, oauthHandler, cancellationToken);
            }

            // Se já está autenticado, conecta diretamente sem abrir janela
            Log("Credenciais encontradas. Conectando diretamente ao Spotify...");
            return CreateClient(config.ClientId, config.ClientSecret, config.RefreshToken);
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

            return CreateClient(config.ClientId, config.ClientSecret, config.RefreshToken);
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

        public async Task<SpotifyClient> CreateClientAsync(string clientId, string clientSecret, string refreshToken)
        {
            var config = SpotifyClientConfig
                .CreateDefault()
                .WithAuthenticator(new AuthorizationCodeAuthenticator(clientId, clientSecret, new AuthorizationCodeTokenResponse
                {
                    RefreshToken = refreshToken
                }));

            var client = new SpotifyClient(config);
            _spotifyService.SetClient(client);

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