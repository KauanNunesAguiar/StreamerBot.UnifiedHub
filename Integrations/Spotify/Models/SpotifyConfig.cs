namespace StreamerBot.UnifiedHub.Integrations.Spotify.Models
{
    public class SpotifyConfig
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
        public string PlaylistId { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public DateTime TokenExpiration { get; set; }
        public bool IsAuthenticated => !string.IsNullOrWhiteSpace(RefreshToken);
    }
}