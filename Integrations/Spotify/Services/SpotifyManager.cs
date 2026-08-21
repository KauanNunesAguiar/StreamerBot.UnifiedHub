using StreamerBot.UnifiedHub.Integrations.Spotify.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyManager(
        SpotifyOAuthHandler oauthHandler,
        SpotifyPlayerService playerService,
        SpotifyConfig config)
    {
        private readonly SpotifyOAuthHandler _oauthHandler = oauthHandler ?? throw new ArgumentNullException(nameof(oauthHandler));
        private readonly SpotifyPlayerService _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
        private readonly SpotifyConfig _config = config ?? throw new ArgumentNullException(nameof(config));

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

        public SpotifyTrackInfo CurrentTrackInfo => _playerService.CurrentTrackInfo;

        /// <summary>
        /// Inicializa o cliente do Spotify (autenticando ou reusando tokens) 
        /// e prepara o PlayerService para uso.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            var spotifyClient = await SpotifyAuthService.EnsureAuthenticatedAsync(_config, _oauthHandler, cancellationToken);
            _playerService.SetClient(spotifyClient);
        }

        /// <summary>
        /// Força a reconfiguração/abertura do navegador para novas credenciais.
        /// </summary>
        public async Task ReconfigureAsync(CancellationToken cancellationToken = default)
        {
            var spotifyClient = await SpotifyAuthService.ReconfigureAsync(_config, _oauthHandler, cancellationToken);
            _playerService.SetClient(spotifyClient);
        }

        #region Repasse de Controles do Player e Fila

        public Task StartPollingAsync(int intervalMilliseconds = 5000, CancellationToken cancellationToken = default)
            => _playerService.StartPollingAsync(intervalMilliseconds, cancellationToken);

        public Task<SpotifyTrackInfo> GetCurrentTrackAsync(CancellationToken cancellationToken = default)
            => _playerService.GetCurrentTrackAsync(cancellationToken);

        public Task PauseAsync() => _playerService.PauseAsync();
        public Task ResumeAsync() => _playerService.ResumeAsync();
        public Task SkipToNextAsync() => _playerService.SkipToNextAsync();
        public Task SkipToPreviousAsync() => _playerService.SkipToPreviousAsync();
        public Task SetVolumeAsync(int volumePercent) => _playerService.SetVolumeAsync(volumePercent);
        public string GetCurrentTrackProgressBar() => _playerService.GetCurrentTrackProgressBar();

        public Task<List<SpotifyTrackInfo>> GetQueueAsync(int limit = 5)
            => _playerService.GetQueueAsync(limit);

        public Task<SpotifyTrackInfo> AddToQueueAsync(string input, string userId, string userName)
            => _playerService.AddToQueueAsync(input, userId, userName);

        public Task<(bool Success, SpotifyTrackInfo? RemovedItem, string Message)> RemoveLastAddedFromQueueAsync(string userId, bool isModOrStreamer = false)
            => _playerService.RemoveLastAddedFromQueueAsync(userId, isModOrStreamer);

        public List<SpotifyTrackInfo> GetPendingUserQueue()
            => _playerService.GetPendingUserQueue();

        #endregion

        #region Playlist de Lives

        /// <summary>
        /// Adiciona a música tocando no momento à playlist de lives configurada (SpotifyConfig.PlaylistId).
        /// </summary>
        public Task<(bool Success, string Message)> AddCurrentTrackToPlaylistAsync(CancellationToken cancellationToken = default)
            => _playerService.AddCurrentTrackToPlaylistAsync(_config.PlaylistId, cancellationToken);

        #endregion

        #region Skip / Voteskip

        /// <summary>
        /// Pula a música atual imediatamente. Restrito a mod/streamer — a checagem de
        /// permissão fica a cargo de quem chama (mesmo padrão de RemoveLastAddedFromQueueAsync).
        /// </summary>
        public Task<(bool Success, string Message)> ForceSkipAsync()
            => _playerService.ForceSkipAsync();

        /// <summary>
        /// Registra o voto de um espectador para pular a música atual. Usa SpotifyConfig.VoteSkipThreshold
        /// como número de votos necessários; quando atingido, a música é pulada automaticamente.
        /// </summary>
        public Task<VoteSkipResult> VoteSkipAsync(string userId, CancellationToken cancellationToken = default)
            => _playerService.RegisterVoteSkipAsync(userId, _config.VoteSkipThreshold, cancellationToken);

        #endregion
    }
}