namespace StreamerBot.UnifiedHub.Integrations.Spotify.Models
{
    public class SpotifyTrackInfo
    {
        public SpotifyIdentifiers Identifiers { get; set; } = new();
        public SpotifyMediaDetails Media { get; set; } = new();
        public SpotifyPlayerState Player { get; set; } = new();
        public SpotifyRequestInfo Request { get; set; } = new();
    }

    public class SpotifyIdentifiers
    {
        public string Id { get; set; } = string.Empty;
        public string Uri { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    public class SpotifyMediaDetails
    {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty; // Nomes concatenados (ex: "Artista 1, Artista 2")
        public string Album { get; set; } = string.Empty;
        public string AlbumArtUrl { get; set; } = string.Empty;
        public bool IsExplicit { get; set; }
    }

    public class SpotifyPlayerState
    {
        public bool IsPlaying { get; set; }
        public long ProgressMs { get; set; }
        public long DurationMs { get; set; }
        public double ProgressPercentage => DurationMs > 0 ? (double)ProgressMs / DurationMs * 100 : 0;
    }

    public class SpotifyRequestInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public bool IsUserRequested => !string.IsNullOrEmpty(UserId);
    }
}