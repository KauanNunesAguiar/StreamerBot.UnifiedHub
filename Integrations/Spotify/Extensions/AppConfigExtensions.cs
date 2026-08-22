using StreamerBot.UnifiedHub.Core.Extensions;
using StreamerBot.UnifiedHub.Core.Models;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Extensions
{
    public static class SpotifyAppConfigExtensions
    {
        private const string SpotifyKey = "Spotify";

        public static SpotifyConfig GetSpotifyConfig(this AppConfig appConfig)
            => appConfig.GetIntegrationConfig<SpotifyConfig>(SpotifyKey);

        public static void SetSpotifyConfig(this AppConfig appConfig, SpotifyConfig config)
            => appConfig.SetIntegrationConfig(SpotifyKey, config);
    }
}