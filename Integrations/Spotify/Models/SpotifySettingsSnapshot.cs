using System;
using System.Collections.Generic;
using System.Text;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Models
{
    public record SpotifySettingsSnapshot(
        string PlaylistId,
        int VoteSkipThreshold,
        int QueueSize,
        string BotLabel,
        int PollingIntervalMs);
}