using Newtonsoft.Json;
using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Models
{
    public class SpotifyConfig : OAuthConfig
    {
        public string PlaylistId { get; set; } = string.Empty;
        public int VoteSkipThreshold { get; set; } = 3;
        public int QueueSize { get; set; } = 5;
        public string BotName { get; set; } = "Spotify";
        public string BotImageUrl { get; set; } = string.Empty;
        public int PollingIntervalMs { get; set; } = 5000;

        // Dicionário de mensagens customizadas pelo usuário (Key -> Template)
        public Dictionary<string, string> Messages { get; set; } = [];

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}