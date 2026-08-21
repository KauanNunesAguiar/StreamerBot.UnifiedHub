namespace StreamerBot.UnifiedHub.Core.Models
{
    public class OAuthResult
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public Dictionary<string, string> ExtraSettings { get; set; } = [];
    }
}