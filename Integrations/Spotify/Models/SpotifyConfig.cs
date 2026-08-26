using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Models
{
    public class SpotifyConfig : ChatIntegrationConfig
    {
        public string PlaylistId { get; set; } = string.Empty;
        public int VoteSkipThreshold { get; set; } = 3;
        public int QueueSize { get; set; } = 5;

        public SpotifyConfig()
        {
            BotLabel = "Spotify";
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}