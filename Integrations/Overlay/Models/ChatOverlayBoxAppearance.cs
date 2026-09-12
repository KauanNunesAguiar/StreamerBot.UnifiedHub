namespace StreamerBot.UnifiedHub.Integrations.Overlay.Models
{
    public class ChatOverlayBoxAppearance
    {
        public string BackgroundColor { get; set; } = "#121212";
        public int BackgroundOpacityPercent { get; set; } = 55;
        public int BorderRadiusPx { get; set; } = 8;
        public int PaddingPx { get; set; } = 6;
        public bool BorderEnabled { get; set; } = false;
        public string BorderColor { get; set; } = "#333333";
        public int BorderWidthPx { get; set; } = 1;
    }
}