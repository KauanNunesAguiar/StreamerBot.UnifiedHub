using System.Collections.Generic;

namespace StreamerBot.UnifiedHub.Integrations.Overlay.Models
{
    public class ChatOverlayProfilesConfig
    {
        public Dictionary<string, ChatOverlayProfile> Profiles { get; set; } = [];
    }
}