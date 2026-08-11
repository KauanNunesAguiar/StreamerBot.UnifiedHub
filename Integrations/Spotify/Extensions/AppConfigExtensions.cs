using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamerBot.UnifiedHub.Core.Models;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Extensions
{
    public static class AppConfigExtensions
    {
        private const string SpotifyKey = "Spotify";

        public static SpotifyConfig GetSpotifyConfig(this AppConfig appConfig)
        {
            if (appConfig.IntegrationSettings.TryGetValue(SpotifyKey, out var value) && value != null)
            {
                if (value is SpotifyConfig config)
                    return config;

                if (value is JObject jObject)
                    return jObject.ToObject<SpotifyConfig>() ?? new SpotifyConfig();

                string json = JsonConvert.SerializeObject(value);
                return JsonConvert.DeserializeObject<SpotifyConfig>(json) ?? new SpotifyConfig();
            }

            var newConfig = new SpotifyConfig();
            appConfig.IntegrationSettings[SpotifyKey] = newConfig;
            return newConfig;
        }

        public static void SetSpotifyConfig(this AppConfig appConfig, SpotifyConfig spotifyConfig)
        {
            appConfig.IntegrationSettings[SpotifyKey] = spotifyConfig;
        }
    }
}