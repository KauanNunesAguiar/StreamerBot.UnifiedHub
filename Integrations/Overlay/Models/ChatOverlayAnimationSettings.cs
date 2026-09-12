namespace StreamerBot.UnifiedHub.Integrations.Overlay.Models
{
    public enum ChatOverlayEntranceAnimation { None, SlideIn, FadeIn }
    public enum ChatOverlayExitAnimation { None, FadeOut, SlideOut }

    public class ChatOverlayAnimationSettings
    {
        public ChatOverlayEntranceAnimation Entrance { get; set; } = ChatOverlayEntranceAnimation.SlideIn;
        public ChatOverlayExitAnimation Exit { get; set; } = ChatOverlayExitAnimation.FadeOut;
        public int AnimationDurationMs { get; set; } = 250;
    }
}