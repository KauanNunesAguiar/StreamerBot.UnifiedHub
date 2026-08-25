using StreamerBot.UnifiedHub.Core.Abstractions;
using StreamerBot.UnifiedHub.Core.Models;
using StreamerBot.UnifiedHub.Core.Services;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyOAuthHandler(
        ILocalHttpServer httpServer,
        IBrowserService browserService,
        IConfigManager? configManager = null)
    {
        private readonly OAuthHandler _oauthHandler = new(httpServer, browserService);
        private readonly IConfigManager? _configManager = configManager;

        public async Task<OAuthResult> AuthenticateUserAsync(
            SpotifyConfig config,
            CancellationToken cancellationToken = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "A configuração do Spotify não pode ser nula.");

            string redirectUri = string.IsNullOrWhiteSpace(config.RedirectUri)
                ? "http://127.0.0.1:5000/callback/"
                : config.RedirectUri;

            var strategy = new SpotifyOAuthStrategy(config, _configManager);

            return await _oauthHandler.AuthenticateUserAsync(strategy, config.ClientId, config.ClientSecret, redirectUri, cancellationToken);
        }
    }
}