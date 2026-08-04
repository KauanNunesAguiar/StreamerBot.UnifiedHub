using System;
using System.Linq;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Integrations
{
    public class SpotifyService
    {
        private SpotifyConfig _config;
        private SpotifyClient _spotify;

        public SpotifyService(SpotifyConfig config)
        {
            _config = config ?? new SpotifyConfig();
        }

        public void UpdateConfig(SpotifyConfig config)
        {
            _config = config;
        }

        public async Task<bool> InitializeAsync()
        {
            if (string.IsNullOrEmpty(_config.RefreshToken) ||
                string.IsNullOrEmpty(_config.ClientId) ||
                string.IsNullOrEmpty(_config.ClientSecret))
            {
                return false;
            }

            try
            {
                var response = await new OAuthClient().RequestToken(
                    new AuthorizationCodeRefreshRequest(_config.ClientId, _config.ClientSecret, _config.RefreshToken)
                );

                var spotifyConfig = SpotifyClientConfig.CreateDefault().WithToken(response.AccessToken);
                _spotify = new SpotifyClient(spotifyConfig);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Spotify] Falha ao inicializar com RefreshToken: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Troca o código pelo Refresh Token. Caso as credenciais sejam inválidas, lança a exceção exata da API.
        /// </summary>
        public async Task<string> ExchangeCodeForRefreshTokenAsync(string clientId, string clientSecret, string code, string redirectUri)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;

            var response = await new OAuthClient().RequestToken(
                new AuthorizationCodeTokenRequest(
                    clientId,
                    clientSecret,
                    code,
                    new Uri(redirectUri)
                )
            );

            return response.RefreshToken;
        }

        public async Task<string> GetCurrentlyPlayingAsync()
        {
            if (_spotify == null) return "Spotify não autenticado";

            try
            {
                var item = await _spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
                if (item?.Item is FullTrack track)
                {
                    string artistas = string.Join(", ", track.Artists.Select(a => a.Name));
                    return $"{track.Name} - {artistas}";
                }

                return "Nenhuma música tocando no momento.";
            }
            catch (Exception ex)
            {
                return $"Erro ao obter música: {ex.Message}";
            }
        }

        public async Task PlayAsync()
        {
            if (_spotify == null) return;
            try { await _spotify.Player.ResumePlayback(); } catch { }
        }

        public async Task PauseAsync()
        {
            if (_spotify == null) return;
            try { await _spotify.Player.PausePlayback(); } catch { }
        }

        public async Task SkipNextAsync()
        {
            if (_spotify == null) return;
            try { await _spotify.Player.SkipNext(); } catch { }
        }
    }
}