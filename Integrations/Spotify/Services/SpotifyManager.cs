using StreamerBot.UnifiedHub.Core.Abstractions;
using StreamerBot.UnifiedHub.Core.Models;
using StreamerBot.UnifiedHub.Core.Services;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyManager(
        SpotifyOAuthHandler oauthHandler,
        SpotifyPlayerService playerService,
        SpotifyConfig config,
        IConfigManager? configManager = null) : IDisposable
    {
        private readonly SpotifyOAuthHandler _oauthHandler = oauthHandler ?? throw new ArgumentNullException(nameof(oauthHandler));
        private readonly SpotifyPlayerService _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
        private readonly SpotifyConfig _config = config ?? throw new ArgumentNullException(nameof(config));
        private readonly IConfigManager? _configManager = configManager;

        public SpotifyTrackInfo CurrentTrackInfo => _playerService.CurrentTrackInfo;

        public event EventHandler<SpotifyTrackInfo>? OnTrackChanged
        {
            add => _playerService.OnTrackChanged += value;
            remove => _playerService.OnTrackChanged -= value;
        }

        public event EventHandler<SpotifyTrackInfo>? OnPlayerUpdated
        {
            add => _playerService.OnPlayerUpdated += value;
            remove => _playerService.OnPlayerUpdated -= value;
        }

        public event EventHandler<ChatMessageEventArgs>? OnChatMessage;

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            var spotifyClient = await SpotifyAuthService.EnsureAuthenticatedAsync(_config, _oauthHandler, _configManager, cancellationToken);
            _playerService.SetClient(spotifyClient);
        }

        public async Task ReconfigureAsync(CancellationToken cancellationToken = default)
        {
            var spotifyClient = await SpotifyAuthService.ReconfigureAsync(_config, _oauthHandler, _configManager, cancellationToken);
            _playerService.SetClient(spotifyClient);
        }

        #region Repasse de Controles do Player e Fila

        public Task StartPollingAsync(int intervalMilliseconds = 5000, CancellationToken cancellationToken = default)
            => _playerService.StartPollingAsync(intervalMilliseconds, cancellationToken);

        public Task<SpotifyTrackInfo> GetCurrentTrackAsync(CancellationToken cancellationToken = default)
            => _playerService.GetCurrentTrackAsync(cancellationToken);

        public async Task PauseAsync(string user = "", CancellationToken cancellationToken = default)
        {
            await _playerService.PauseAsync(cancellationToken);
            RaiseChatMessage(SpotifyMessageCatalog.Keys.Pause, new() { ["user"] = user });
        }

        public async Task ResumeAsync(string user = "", CancellationToken cancellationToken = default)
        {
            await _playerService.ResumeAsync(cancellationToken);
            RaiseChatMessage(SpotifyMessageCatalog.Keys.Play, new() { ["user"] = user });
        }

        public async Task SkipToNextAsync(string user = "", CancellationToken cancellationToken = default)
        {
            await _playerService.SkipToNextAsync(cancellationToken);
            await RaiseSkipChatMessageAsync(SpotifyMessageCatalog.Keys.Next, user, cancellationToken);
        }

        public async Task SkipToPreviousAsync(string user = "", CancellationToken cancellationToken = default)
        {
            await _playerService.SkipToPreviousAsync(cancellationToken);
            await RaiseSkipChatMessageAsync(SpotifyMessageCatalog.Keys.Prev, user, cancellationToken);
        }

        public async Task SetVolumeAsync(int volumePercent, string user = "", CancellationToken cancellationToken = default)
        {
            await _playerService.SetVolumeAsync(volumePercent, cancellationToken);
            RaiseChatMessage(SpotifyMessageCatalog.Keys.Volume, new() { ["user"] = user, ["volume"] = volumePercent.ToString() });
        }

        public string GetCurrentTrackProgressBar() => _playerService.GetCurrentTrackProgressBar();

        public Task<List<SpotifyTrackInfo>> GetQueueAsync(int limit = 5, CancellationToken cancellationToken = default)
            => _playerService.GetQueueAsync(limit, cancellationToken);

        public async Task<SpotifyTrackInfo> AddToQueueAsync(string input, string userId, string userName, CancellationToken cancellationToken = default)
        {
            var track = await _playerService.AddToQueueAsync(input, userId, userName, cancellationToken);
            RaiseChatMessage(SpotifyMessageCatalog.Keys.AddToQueue, new()
            {
                ["user"] = userName,
                ["musica"] = track.Media.Title,
                ["artista"] = track.Media.Artist,
                ["link_musica"] = track.Identifiers.Url
            });
            return track;
        }

        public Task<(bool Success, SpotifyTrackInfo? RemovedItem, string Message)> RemoveLastAddedFromQueueAsync(string userId, bool isModOrStreamer = false, CancellationToken cancellationToken = default)
            => _playerService.RemoveLastAddedFromQueueAsync(userId, isModOrStreamer, cancellationToken);

        public List<SpotifyTrackInfo> GetPendingUserQueue()
            => _playerService.GetPendingUserQueue();

        private void RaiseChatMessage(string key, Dictionary<string, string>? placeholders = null)
        {
            if (!_config.Messages.TryGetValue(key, out string? template) || string.IsNullOrWhiteSpace(template))
                return;

            string message = ChatMessageFormatter.Format(template, placeholders ?? []);
            OnChatMessage?.Invoke(this, new ChatMessageEventArgs(_config.BotName, message));
        }

        private async Task RaiseSkipChatMessageAsync(string key, string user, CancellationToken cancellationToken)
        {
            await Task.Delay(700, cancellationToken); // Spotify leva um instante para refletir a nova faixa
            var track = await _playerService.GetCurrentTrackAsync(cancellationToken);

            RaiseChatMessage(key, new()
            {
                ["user"] = user,
                ["musica"] = track.Media.Title,
                ["artista"] = track.Media.Artist
            });
        }

        #endregion

        #region Playlist de Lives

        public async Task<(bool Success, string Message)> AddCurrentTrackToPlaylistAsync(string user = "", CancellationToken cancellationToken = default)
        {
            var track = CurrentTrackInfo;
            var result = await _playerService.AddCurrentTrackToPlaylistAsync(_config.PlaylistId, cancellationToken);
            if (result.Success)
                RaiseChatMessage(SpotifyMessageCatalog.Keys.AddToPlaylist, new() { ["user"] = user, ["musica"] = track.Media.Title });
            return result;
        }

        #endregion

        #region Skip / Voteskip

        public async Task<(bool Success, string Message)> ForceSkipAsync(string user = "", CancellationToken cancellationToken = default)
        {
            var result = await _playerService.ForceSkipAsync(cancellationToken);
            if (result.Success)
                RaiseChatMessage(SpotifyMessageCatalog.Keys.ForceSkip, new() { ["user"] = user });
            return result;
        }

        public async Task<VoteSkipResult> VoteSkipAsync(string userId, CancellationToken cancellationToken = default)
        {
            var result = await _playerService.RegisterVoteSkipAsync(userId, _config.VoteSkipThreshold, cancellationToken);
            RaiseChatMessage(SpotifyMessageCatalog.Keys.VoteSkip, new()
            {
                ["user"] = userId,
                ["votos_atuais"] = result.CurrentVotes.ToString(),
                ["votos_necessarios"] = result.RequiredVotes.ToString()
            });
            return result;
        }

        #endregion

        public void Dispose()
        {
            _playerService?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}