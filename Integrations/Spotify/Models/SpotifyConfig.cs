using StreamerBot.UnifiedHub.Core.Models;
namespace StreamerBot.UnifiedHub.Integrations.Spotify.Models
{
    public class SpotifyConfig : OAuthConfig
    {
        public string PlaylistId { get; set; } = string.Empty;
    }
}