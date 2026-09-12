namespace StreamerBot.UnifiedHub.Integrations.Overlay.Models
{
    public class ChatOverlayBackgroundAppearance
    {
        public bool Enabled { get; set; } = false;
        public string Color { get; set; } = "#000000";
        public int OpacityPercent { get; set; } = 0;
        public int BorderRadiusPx { get; set; } = 0;
    }
}