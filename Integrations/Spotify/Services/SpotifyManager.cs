using StreamerBot.UnifiedHub.Integrations.Spotify.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyManager(
        SpotifyAuthService authService,
        SpotifyOAuthHandler oauthHandler,
        SpotifyPlayerService playerService)
    {
        private readonly SpotifyAuthService _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        private readonly SpotifyOAuthHandler _oauthHandler = oauthHandler ?? throw new ArgumentNullException(nameof(oauthHandler));
        private readonly SpotifyPlayerService _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));

        public SpotifyPlayerService Player => _playerService;

        public async Task InitializeAsync(SpotifyConfig config, CancellationToken cancellationToken = default)
        {
            var client = await _authService.EnsureAuthenticatedAsync(config, _oauthHandler, cancellationToken);
            _playerService.SetClient(client);
        }
    }
}