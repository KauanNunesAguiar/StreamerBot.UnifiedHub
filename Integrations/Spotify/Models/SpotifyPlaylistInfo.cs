using System;
using System.Collections.Generic;
using System.Text;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Models
{
    public class SpotifyPlaylistInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public int TracksTotal { get; set; }
    }
}