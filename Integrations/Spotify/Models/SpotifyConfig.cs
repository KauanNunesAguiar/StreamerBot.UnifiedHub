using Newtonsoft.Json;
using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Models
{
    public class SpotifyConfig : OAuthConfig
    {
        public string PlaylistId { get; set; } = string.Empty;
        public int VoteSkipThreshold { get; set; } = 3;

        // Dicionário de mensagens customizadas pelo usuário (Key -> Template)
        public Dictionary<string, string> Messages { get; set; } = new();

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}