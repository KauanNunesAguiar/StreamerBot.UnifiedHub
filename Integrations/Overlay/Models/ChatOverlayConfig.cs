using Newtonsoft.Json;

namespace StreamerBot.UnifiedHub.Integrations.Overlay.Models
{
    public class ChatOverlayConfig
    {
        public int Port { get; set; } = 8081;
        public string Endpoint { get; set; } = "/ws";
        public int MaxMessages { get; set; } = 50;
        public int FadeTimeMs { get; set; } = 12000;
        public int EmoteSize { get; set; } = 28;
        public int BadgeSize { get; set; } = 18;
        public bool ShowBadges { get; set; } = true;
        public ChatOverlayMode Mode { get; set; } = ChatOverlayMode.FadeOut;

        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented);
    }
}