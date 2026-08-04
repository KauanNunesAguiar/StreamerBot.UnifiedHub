using Newtonsoft.Json;

namespace StreamerBot.UnifiedHub.Core.Models
{
    public class SpotifyConfig
    {
        [JsonProperty("spotifyClientId")]
        //public string ClientId { get; set; } = "4b1b5d63e7624ef4916023d360251577";
        public string ClientId { get; set; } = string.Empty;

        [JsonProperty("spotifyClientSecret")]
        //public string ClientSecret { get; set; } = "50f796ba7e6f4050a0c0e92d0a9e9139";
        public string ClientSecret { get; set; } = string.Empty;

        [JsonProperty("spotifyRefreshToken")]
        public string RefreshToken { get; set; } = string.Empty;

        [JsonProperty("redirectUri")]
        public string RedirectUri { get; set; } = "http://127.0.0.1:8888/callback/";
    }
}