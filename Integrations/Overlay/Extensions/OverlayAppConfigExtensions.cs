using StreamerBot.UnifiedHub.Core.Extensions;
using StreamerBot.UnifiedHub.Core.Models;
using StreamerBot.UnifiedHub.Integrations.Overlay.Models;

namespace StreamerBot.UnifiedHub.Integrations.Overlay.Extensions
{
    public static class OverlayAppConfigExtensions
    {
        private const string OverlayKey = "ChatOverlay";

        public static ChatOverlayConfig GetChatOverlayConfig(this AppConfig appConfig)
            => appConfig.GetIntegrationConfig<ChatOverlayConfig>(OverlayKey);

        public static void SetChatOverlayConfig(this AppConfig appConfig, ChatOverlayConfig config)
            => appConfig.SetIntegrationConfig(OverlayKey, config);
    }
}