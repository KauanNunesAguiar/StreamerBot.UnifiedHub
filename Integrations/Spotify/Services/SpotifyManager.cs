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
            _playerService.OnTrackChanged += HandleTrackChangedForNewTrackMessage;

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

        public async Task<SpotifyTrackInfo> GetCurrentTrackAsync(CancellationToken cancellationToken = default)
        {
            var track = await _playerService.GetCurrentTrackAsync(cancellationToken);

            if (track.Player.IsPlaying && !string.IsNullOrEmpty(track.Identifiers.Uri))
            {
                RaiseChatMessage(SpotifyMessageCatalog.Keys.CurrentTrack, new()
                {
                    ["musica"] = track.Media.Title,
                    ["artista"] = track.Media.Artist,
                    ["album"] = track.Media.Album,
                    ["progresso"] = FormatProgress(track.Player.ProgressMs, track.Player.DurationMs),
                    ["link_musica"] = track.Identifiers.Url
                });
            }
            else
            {
                RaiseChatMessage(SpotifyMessageCatalog.Keys.NothingPlaying, new());
            }

            return track;
        }

        private static string FormatProgress(long progressMs, long durationMs)
        {
            TimeSpan progress = TimeSpan.FromMilliseconds(progressMs);
            TimeSpan duration = TimeSpan.FromMilliseconds(durationMs);
            return $"{progress:mm\\:ss} / {duration:mm\\:ss}";
        }

        private void HandleTrackChangedForNewTrackMessage(object? sender, SpotifyTrackInfo track)
        {
            if (!track.Player.IsPlaying || string.IsNullOrEmpty(track.Identifiers.Uri))
                return; // ignora o reset (nada tocando)

            string key = track.Request.IsUserRequested
                ? SpotifyMessageCatalog.Keys.NewByRequest
                : SpotifyMessageCatalog.Keys.New;

            RaiseChatMessage(key, new()
            {
                ["user"] = track.Request.IsUserRequested ? track.Request.UserName : string.Empty,
                ["musica"] = track.Media.Title,
                ["artista"] = track.Media.Artist,
                ["album"] = track.Media.Album,
                ["link_musica"] = track.Identifiers.Url
            });
        }

        public async Task PauseAsync(string user = "", CancellationToken cancellationToken = default)
        {
            if (!CurrentTrackInfo.Player.IsPlaying)
            {
                RaiseChatMessage(SpotifyMessageCatalog.Keys.AlreadyPaused, new() { ["user"] = user });
                return;
            }

            await _playerService.PauseAsync(cancellationToken);
            RaiseChatMessage(SpotifyMessageCatalog.Keys.Pause, new() { ["user"] = user });
        }

        public async Task ResumeAsync(string user = "", CancellationToken cancellationToken = default)
        {
            if (CurrentTrackInfo.Player.IsPlaying)
            {
                RaiseChatMessage(SpotifyMessageCatalog.Keys.AlreadyPlaying, new() { ["user"] = user });
                return;
            }

            await _playerService.ResumeAsync(cancellationToken);
            RaiseChatMessage(SpotifyMessageCatalog.Keys.Play, new() { ["user"] = user });
        }

        public async Task SetVolumeAsync(int volumePercent, string user = "", CancellationToken cancellationToken = default)
        {
            await _playerService.SetVolumeAsync(volumePercent, cancellationToken);
            RaiseChatMessage(SpotifyMessageCatalog.Keys.Volume, new() { ["user"] = user, ["volume"] = volumePercent.ToString() });
        }

        public string GetCurrentTrackProgressBar() => _playerService.GetCurrentTrackProgressBar();

        public async Task<List<SpotifyTrackInfo>> GetQueueAsync(int? limit = null, CancellationToken cancellationToken = default)
        {
            var queue = await _playerService.GetQueueAsync(limit ?? _config.QueueSize, cancellationToken);

            string listaFila = queue.Count > 0
                ? string.Join(" • ", queue.Select((t, i) => $"#{i + 1} {t.Media.Title} ({t.Media.Artist})"))
                : "Fila vazia";

            RaiseChatMessage(SpotifyMessageCatalog.Keys.Queue, new() { ["lista_fila"] = listaFila });

            return queue;
        }

        public async Task<SpotifyTrackInfo> AddToQueueAsync(string input, string userId, string userName, CancellationToken cancellationToken = default)
        {
            SpotifyTrackInfo track;
            try
            {
                track = await _playerService.AddToQueueAsync(input, userId, userName, cancellationToken);
            }
            catch (Exception)
            {
                RaiseChatMessage(SpotifyMessageCatalog.Keys.AddNotFound, new() { ["user"] = userName });
                throw;
            }

            int posicao = _playerService.GetPendingUserQueue().Count;

            RaiseChatMessage(SpotifyMessageCatalog.Keys.AddToQueue, new()
            {
                ["user"] = userName,
                ["musica"] = track.Media.Title,
                ["artista"] = track.Media.Artist,
                ["link_musica"] = track.Identifiers.Url,
                ["posicao"] = posicao.ToString()
            });
            return track;
        }

        public async Task<(bool Success, SpotifyTrackInfo? RemovedItem, string Message)> RemoveLastAddedFromQueueAsync(string userId, bool isModOrStreamer = false, CancellationToken cancellationToken = default)
        {
            var result = await _playerService.RemoveLastAddedFromQueueAsync(userId, isModOrStreamer, cancellationToken);

            if (result.Success && result.RemovedItem != null)
            {
                RaiseChatMessage(SpotifyMessageCatalog.Keys.Undo, new()
                {
                    ["user"] = result.RemovedItem.Request?.UserName ?? string.Empty,
                    ["musica"] = result.RemovedItem.Media.Title
                });
            }
            else
            {
                RaiseChatMessage(SpotifyMessageCatalog.Keys.UndoEmpty, new() { ["user"] = userId });
            }

            return result;
        }

        public List<SpotifyTrackInfo> GetPendingUserQueue()
            => _playerService.GetPendingUserQueue();

        private void RaiseChatMessage(string key, Dictionary<string, string>? placeholders = null)
        {
            if (_config.MessageEnabled.TryGetValue(key, out bool isEnabled) && !isEnabled)
                return; // usuário desativou essa mensagem

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
                ["artista"] = track.Media.Artist,
                ["link_musica"] = track.Identifiers.Url
            });
        }

        #endregion

        #region Playlist de Lives
        public Task ShowPlaylistInfoAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_config.PlaylistId))
                throw new InvalidOperationException("Nenhuma playlist de lives configurada. Rode 'config' para escolher uma.");

            string link = $"https://open.spotify.com/playlist/{_config.PlaylistId}";
            RaiseChatMessage(SpotifyMessageCatalog.Keys.Playlist, new() { ["playlist_link"] = link });
            return Task.CompletedTask;
        }

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
            var current = CurrentTrackInfo;
            var result = await _playerService.ForceSkipAsync(cancellationToken);
            if (result.Success)
            {
                RaiseChatMessage(SpotifyMessageCatalog.Keys.ForceSkip, new()
                {
                    ["user"] = user,
                    ["musica"] = current.Media.Title,
                    ["artista"] = current.Media.Artist,
                    ["link_musica"] = current.Identifiers.Url
                });
            }
            return result;
        }

        public async Task SkipToPreviousAsync(string user = "", CancellationToken cancellationToken = default)
        {
            await _playerService.SkipToPreviousAsync(cancellationToken);
            await RaiseSkipChatMessageAsync(SpotifyMessageCatalog.Keys.Prev, user, cancellationToken);
        }

        public async Task<VoteSkipResult> VoteSkipAsync(string userId, CancellationToken cancellationToken = default)
        {
            var current = CurrentTrackInfo;

            if (!current.Player.IsPlaying || string.IsNullOrEmpty(current.Identifiers.Uri))
            {
                RaiseChatMessage(SpotifyMessageCatalog.Keys.NothingPlaying, new() { ["user"] = userId });
                return new VoteSkipResult(false, "Nenhuma música tocando no momento para votar.", 0, _config.VoteSkipThreshold, false);
            }

            var result = await _playerService.RegisterVoteSkipAsync(userId, _config.VoteSkipThreshold, cancellationToken);

            string key = result.AlreadyVoted
                ? SpotifyMessageCatalog.Keys.JaVotou
                : SpotifyMessageCatalog.Keys.VoteSkip;

            RaiseChatMessage(key, new()
            {
                ["user"] = userId,
                ["musica"] = current.Media.Title,
                ["artista"] = current.Media.Artist,
                ["link_musica"] = current.Identifiers.Url,
                ["votos_atuais"] = result.CurrentVotes.ToString(),
                ["votos_necessarios"] = result.RequiredVotes.ToString()
            });
            return result;
        }

        #endregion

        public void NotifyNoPermission(string user = "")
        {
            RaiseChatMessage(SpotifyMessageCatalog.Keys.NoPermission, new() { ["user"] = user });
        }

        public void ShowHelp(string user = "", string listaComandos = "")
        {
            RaiseChatMessage(SpotifyMessageCatalog.Keys.SongHelp, new()
            {
                ["user"] = user,
                ["lista_comandos"] = listaComandos
            });
        }

        public void Dispose()
        {
            _playerService?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}