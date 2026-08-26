namespace StreamerBot.UnifiedHub.Core.Models
{
    /// <summary>
    /// Config base para integrações que falam no chat (Spotify, Twitch, YouTube...).
    /// Reúne identidade do bot, intervalo de polling e catálogo de mensagens.
    /// </summary>
    public class ChatIntegrationConfig : OAuthConfig
    {
        public string BotLabel { get; set; } = string.Empty;
        public int PollingIntervalMs { get; set; } = 5000;

        public Dictionary<string, string> Messages { get; set; } = [];
        public Dictionary<string, bool> MessageEnabled { get; set; } = [];
    }
}