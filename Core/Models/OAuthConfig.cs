namespace StreamerBot.UnifiedHub.Core.Models
{
    public class OAuthConfig
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = "http://127.0.0.1:5000/callback/";
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime TokenExpiration { get; set; }

        public virtual bool IsAuthenticated => !string.IsNullOrWhiteSpace(RefreshToken);
    }
}