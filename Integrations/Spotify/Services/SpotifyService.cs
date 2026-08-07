using System;
using System.Threading;
using System.Threading.Tasks;
using SpotifyAPI.Web;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyService
    {
        // Adicionada a interrogação (?) para indicar que pode ser null antes do InitializeAsync
        private SpotifyClient? _spotifyClient;
        private string _lastTrackId = string.Empty;

        // Adicionada a interrogação (?) para indicar que o evento pode ser null (sem assinantes)
        public event EventHandler<FullTrack>? OnTrackChanged;

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