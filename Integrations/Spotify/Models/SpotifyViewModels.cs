namespace StreamerBot.UnifiedHub.Integrations.Spotify.Models
{
    public class SpotifyLoginViewModel
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string? Error { get; set; }
    }

    public class SpotifyMessageInputViewModel
    {
        public MessageDefinition Definition { get; set; } = new();
        public string Value { get; set; } = string.Empty;
    }

    public class SpotifySettingsViewModel
    {
        public string? Error { get; set; }
        public List<SpotifyPlaylistInfo> Playlists { get; set; } = [];
        public string SelectedPlaylistId { get; set; } = string.Empty;
        public int VoteSkipThreshold { get; set; } = 3;
        public List<SpotifyMessageInputViewModel> Messages { get; set; } = [];
    }
}