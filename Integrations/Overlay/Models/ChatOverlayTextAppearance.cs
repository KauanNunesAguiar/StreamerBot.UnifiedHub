namespace StreamerBot.UnifiedHub.Integrations.Overlay.Models
{
    public class ChatOverlayTextAppearance
    {
        public string FontFamily { get; set; } = "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif";
        public int FontSizePx { get; set; } = 18;
        public string FontWeight { get; set; } = "400";
        public string TextColor { get; set; } = "#FFFFFF";

        /// <summary>Se true, usa a cor vinda da plataforma (Twitch/YouTube) para o nome do usuário; se false, usa TextColor pra tudo.</summary>
        public bool UseUserColor { get; set; } = true;
        public bool OutlineEnabled { get; set; } = false;
        public string OutlineColor { get; set; } = "#000000";
    }
}