namespace StreamerBot.UnifiedHub.Core.Models
{
    public class OAuthLoginViewModel
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string? Error { get; set; }
    }
}