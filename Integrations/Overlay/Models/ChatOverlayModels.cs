namespace StreamerBot.UnifiedHub.Integrations.Overlay.Models
{
    public enum ChatOverlayMode
    {
        FadeOut,
        Permanent
    }

    public record ChatOverlayMessage(
    string Platform,
    string UserName,
    string Message,
    string? Color,
    string? Emotes,
    bool IsBroadcaster,
    bool IsModerator,
    bool IsVip,
    bool IsSubscriber,
    DateTime Timestamp);

    public class OverlaySettingsViewModel
    {
        public ChatOverlayConfig Config { get; set; } = new();
        public string? Error { get; set; }
        public bool Saved { get; set; }
    }
}