namespace StreamerBot.UnifiedHub.Integrations.Spotify.Models
{
    public class SpotifyTrackInfo
    {
        public string TrackName { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string AlbumName { get; set; } = string.Empty;
        public string AlbumArtUrl { get; set; } = string.Empty;
        public bool IsPlaying { get; set; }
        public long ProgressMs { get; set; }
        public long DurationMs { get; set; }
    }
}