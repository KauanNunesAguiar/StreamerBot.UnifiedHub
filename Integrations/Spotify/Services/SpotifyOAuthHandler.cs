using StreamerBot.UnifiedHub.Core.Abstractions;
using StreamerBot.UnifiedHub.Core.Services;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyOAuthHandler
    {
        private readonly ILocalHttpServer _httpServer;
        private readonly IBrowserService _browserService;
        private readonly SpotifyService _spotifyService;

        public SpotifyOAuthHandler(
            ILocalHttpServer httpServer,
            IBrowserService browserService,
            SpotifyService spotifyService)
        {
            _httpServer = httpServer;
            _browserService = browserService;
            _spotifyService = spotifyService;
        }

        public async Task<(string? clientId, string? clientSecret, string? refreshToken)> AuthenticateUserAsync(
            SpotifyConfig config,
            CancellationToken cancellationToken = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "A configuração do Spotify não pode ser nula.");

            string redirectUri = string.IsNullOrWhiteSpace(config.RedirectUri)
                ? "http://127.0.0.1:5000/callback/"
                : config.RedirectUri;

            var strategy = new SpotifyOAuthStrategy(_spotifyService);
            var flowHandler = new OAuthFlowHandler(_httpServer, _browserService, strategy);

            var result = await flowHandler.RunAsync(config.ClientId, config.ClientSecret, redirectUri, cancellationToken);

            return (result.ClientId, result.ClientSecret, result.RefreshToken);
        }
    }
}