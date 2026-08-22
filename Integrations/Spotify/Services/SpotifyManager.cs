using StreamerBot.UnifiedHub.Core.Abstractions;
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

        public Task PauseAsync(CancellationToken cancellationToken = default)
            => _playerService.PauseAsync(cancellationToken);

        public Task ResumeAsync(CancellationToken cancellationToken = default)
            => _playerService.ResumeAsync(cancellationToken);

        public Task SkipToNextAsync(CancellationToken cancellationToken = default)
            => _playerService.SkipToNextAsync(cancellationToken);

        public Task SkipToPreviousAsync(CancellationToken cancellationToken = default)
            => _playerService.SkipToPreviousAsync(cancellationToken);

        public Task SetVolumeAsync(int volumePercent, CancellationToken cancellationToken = default)
            => _playerService.SetVolumeAsync(volumePercent, cancellationToken);

        public string GetCurrentTrackProgressBar() => _playerService.GetCurrentTrackProgressBar();

        public Task<List<SpotifyTrackInfo>> GetQueueAsync(int limit = 5, CancellationToken cancellationToken = default)
            => _playerService.GetQueueAsync(limit, cancellationToken);

        public Task<SpotifyTrackInfo> AddToQueueAsync(string input, string userId, string userName, CancellationToken cancellationToken = default)
            => _playerService.AddToQueueAsync(input, userId, userName, cancellationToken);

        public Task<(bool Success, SpotifyTrackInfo? RemovedItem, string Message)> RemoveLastAddedFromQueueAsync(string userId, bool isModOrStreamer = false, CancellationToken cancellationToken = default)
            => _playerService.RemoveLastAddedFromQueueAsync(userId, isModOrStreamer, cancellationToken);

        public List<SpotifyTrackInfo> GetPendingUserQueue()
            => _playerService.GetPendingUserQueue();

        #endregion

        #region Playlist de Lives

        public Task<(bool Success, string Message)> AddCurrentTrackToPlaylistAsync(CancellationToken cancellationToken = default)
            => _playerService.AddCurrentTrackToPlaylistAsync(_config.PlaylistId, cancellationToken);

        #endregion

        #region Skip / Voteskip

        public Task<(bool Success, string Message)> ForceSkipAsync(CancellationToken cancellationToken = default)
            => _playerService.ForceSkipAsync(cancellationToken);

        public Task<VoteSkipResult> VoteSkipAsync(string userId, CancellationToken cancellationToken = default)
            => _playerService.RegisterVoteSkipAsync(userId, _config.VoteSkipThreshold, cancellationToken);

        #endregion

        public void Dispose()
        {
            _playerService?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}