using System;
using System.Threading;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyService
    {
        private SpotifyClient? _spotifyClient;
        private string _lastTrackId = string.Empty;

        public event EventHandler<FullTrack>? OnTrackChanged;

        /// <summary>
        /// Garante a conexão. Se o usuário já tiver o RefreshToken, conecta diretamente.
        /// Caso contrário (primeiro acesso), abre o painel no navegador.
        /// </summary>
        public async Task EnsureAuthenticatedAsync(
            SpotifyConfig config,
            SpotifyOAuthHandler oauthHandler,
            CancellationToken cancellationToken = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "A configuração do Spotify não pode ser nula.");

            // Se ainda não tem autenticação salva (primeira vez do amigo/usuário)
            if (!config.IsAuthenticated)
            {
                Console.WriteLine("[SpotifyService] Nenhuma configuração encontrada. Abrindo painel no navegador...");
                await ReconfigureAsync(config, oauthHandler, cancellationToken);
                return;
            }

            // Se já está autenticado, conecta diretamente sem abrir janela
            Console.WriteLine("[SpotifyService] Credenciais encontradas. Conectando diretamente ao Spotify...");
            await InitializeAsync(config.ClientId, config.ClientSecret, config.RefreshToken);
        }

        /// <summary>
        /// Força a abertura da página no navegador para alterar Client ID / Secret, 
        /// mantendo os dados anteriores pré-preenchidos.
        /// </summary>
        public async Task ReconfigureAsync(
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

            await InitializeAsync(config.ClientId, config.ClientSecret, config.RefreshToken);
        }

        public async Task<string> ExchangeCodeForRefreshTokenAsync(
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

        public async Task InitializeAsync(string clientId, string clientSecret, string refreshToken)
        {
            var config = SpotifyClientConfig
                .CreateDefault()
                .WithAuthenticator(new AuthorizationCodeAuthenticator(clientId, clientSecret, new AuthorizationCodeTokenResponse
                {
                    RefreshToken = refreshToken
                }));

            _spotifyClient = new SpotifyClient(config);

            var me = await _spotifyClient.UserProfile.Current();
            Console.WriteLine($"[SpotifyService] Conectado com sucesso como: {me.DisplayName} ({me.Id})");
        }

        public async Task StartPollingAsync(int intervalMilliseconds = 3000, CancellationToken cancellationToken = default)
        {
            if (_spotifyClient == null)
                throw new InvalidOperationException("O SpotifyService precisa ser inicializado antes de iniciar o monitoramento.");

            Console.WriteLine("[SpotifyService] Monitoramento de músicas iniciado...");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var currentlyPlaying = await _spotifyClient.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());

                    if (currentlyPlaying?.Item is FullTrack track)
                    {
                        if (track.Id != _lastTrackId)
                        {
                            _lastTrackId = track.Id;
                            OnTrackChanged?.Invoke(this, track);
                        }
                    }
                }
                catch (APIException ex)
                {
                    Console.WriteLine($"[SpotifyService] Erro na API do Spotify: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SpotifyService] Erro ao buscar reprodução: {ex.Message}");
                }

                await Task.Delay(intervalMilliseconds, cancellationToken);
            }
        }
    }
}