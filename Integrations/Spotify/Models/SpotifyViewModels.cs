using System;
using System.Collections.Generic;
using System.Text;
using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Models
{
    public class SpotifySettingsViewModel
    {
        public string? Error { get; set; }
        public List<SpotifyPlaylistInfo> Playlists { get; set; } = [];
        public string SelectedPlaylistId { get; set; } = string.Empty;
        public int VoteSkipThreshold { get; set; } = 3;
        public int QueueSize { get; set; } = 5;
        public string BotLabel { get; set; } = "Spotify";
        public int PollingIntervalMs { get; set; } = 5000;
        public List<MessageInputViewModel> Messages { get; set; } = [];
    }
}