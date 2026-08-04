using Newtonsoft.Json;

namespace StreamerBot.UnifiedHub.Core.Models
{
    public class AppConfig
    {
        [JsonProperty("spotify")]
        public SpotifyConfig Spotify { get; set; } = new SpotifyConfig();
    }
}