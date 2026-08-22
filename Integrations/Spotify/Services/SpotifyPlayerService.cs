using System.Collections.Concurrent;
using SpotifyAPI.Web;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;
using StreamerBot.UnifiedHub.Integrations.Youtube.Services;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyPlayerService(YouTubeService youTubeService) : IDisposable
    {
        #region Fields

        private bool _disposed;
        private readonly YouTubeService _youTubeService = youTubeService ?? throw new ArgumentNullException(nameof(youTubeService));
        private SpotifyClient? _spotifyClient;
        private string _lastTrackUri = string.Empty;

        private readonly ConcurrentDictionary<string, byte> _canceledTrackUris = new();
        private readonly List<SpotifyTrackInfo> _userRequestedQueue = [];
        private readonly SemaphoreSlim _playerLock = new(1, 1);

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _voteSkipTracker = new();

        #endregion

        #region Properties & Events

        public SpotifyTrackInfo CurrentTrackInfo { get; private set; } = new SpotifyTrackInfo();

        public event EventHandler<SpotifyTrackInfo>? OnTrackChanged;
        public event EventHandler<SpotifyTrackInfo>? OnPlayerUpdated;

        #endregion

        #region Initialization

        /// <summary>
        /// Define a instância já autenticada do cliente do Spotify.
        /// </summary>
        public void SetClient(SpotifyClient spotifyClient)
        {
            _spotifyClient = spotifyClient ?? throw new ArgumentNullException(nameof(spotifyClient));
        }

        #endregion

        #region Public Methods - Player Control

        public async Task StartPollingAsync(int intervalMilliseconds = 5000, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            Log("Monitoramento de músicas iniciado...");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    (CurrentlyPlaying? currentlyPlaying, FullTrack? track) = (null, null);

                    await _playerLock.WaitAsync(cancellationToken);
                    try
                    {
                        (currentlyPlaying, track) = await FetchCurrentlyPlayingAsync(cancellationToken);

                        if (currentlyPlaying != null && track != null)
                        {
                            if (_canceledTrackUris.ContainsKey(track.Uri))
                            {
                                Log($"[UNDO AUTO] A música '{track.Name}' foi desfeita. Pulando automaticamente...");
                                _canceledTrackUris.TryRemove(track.Uri, out _);

                                await SkipToNextAsync();

                                lock (_userRequestedQueue)
                                {
                                    _userRequestedQueue.RemoveAll(x => x.Identifiers?.Uri == track.Uri);
                                }

                                await Task.Delay(1000, cancellationToken);
                                continue;
                            }

                            UpdateCurrentTrackState(currentlyPlaying, track);
                        }
                    }
                    finally
                    {
                        _playerLock.Release();
                    }

                    if (currentlyPlaying != null && track != null)
                    {
                        string progressBar = GenerateProgressBar(CurrentTrackInfo.Player.ProgressMs, CurrentTrackInfo.Player.DurationMs);
                        Log($"Tocando agora: {track.Name} - {progressBar}");
                    }

                    await Task.Delay(intervalMilliseconds, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    Log("Monitoramento de músicas encerrado com sucesso.");
                    break;
                }
                catch (APIException ex) when ((int?)ex.Response?.StatusCode == 429)
                {
                    int retryAfterSeconds = 30;

                    if (ex.Response?.Headers != null &&
                        ex.Response.Headers.TryGetValue("Retry-After", out string? retryHeader) &&
                        int.TryParse(retryHeader, out int parsedSeconds))
                    {
                        retryAfterSeconds = parsedSeconds;
                    }

                    Log($"⚠️ Limite de requisições excedido (Rate Limit Spotify). Aguardando {retryAfterSeconds}s antes de tentar novamente...");

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(retryAfterSeconds), cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                catch (APIException ex) when (ex.Response?.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    ResetCurrentTrackInfo();
                    await Task.Delay(intervalMilliseconds, cancellationToken);
                }
                catch (APIException ex)
                {
                    Log($"Erro na API do Spotify ({ex.Response?.StatusCode}): {ex.Message}");
                    await Task.Delay(intervalMilliseconds, cancellationToken);
                }
                catch (Exception ex)
                {
                    Log($"Erro ao buscar reprodução: {ex.Message}");
                    await Task.Delay(intervalMilliseconds, cancellationToken);
                }
            }
        }

        public async Task<SpotifyTrackInfo> GetCurrentTrackAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            var (currentlyPlaying, track) = await FetchCurrentlyPlayingAsync(cancellationToken);

            if (currentlyPlaying != null && track != null)
                return UpdateCurrentTrackState(currentlyPlaying, track);

            ResetCurrentTrackInfo();
            return CurrentTrackInfo;
        }

        public async Task PauseAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            await _spotifyClient!.Player.PausePlayback(cancellationToken);

            CurrentTrackInfo.Player.IsPlaying = false;
            OnPlayerUpdated?.Invoke(this, CurrentTrackInfo);
        }

        public async Task ResumeAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            await _spotifyClient!.Player.ResumePlayback(cancellationToken);

            CurrentTrackInfo.Player.IsPlaying = true;
            OnPlayerUpdated?.Invoke(this, CurrentTrackInfo);
        }

        public async Task SkipToNextAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            await _spotifyClient!.Player.SkipNext(cancellationToken);
        }

        public async Task SkipToPreviousAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            await _spotifyClient!.Player.SkipPrevious(cancellationToken);
        }

        public async Task SetVolumeAsync(int volumePercent, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            int volume = Math.Clamp(volumePercent, 0, 100);
            await _spotifyClient!.Player.SetVolume(new PlayerVolumeRequest(volume), cancellationToken);
        }

        public string GetCurrentTrackProgressBar()
        {
            if (CurrentTrackInfo == null || CurrentTrackInfo.Player.DurationMs <= 0)
                return "Nenhuma música tocando no momento.";

            return GenerateProgressBar(CurrentTrackInfo.Player.ProgressMs, CurrentTrackInfo.Player.DurationMs);
        }

        public async Task<(bool Success, string Message)> ForceSkipAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            var current = CurrentTrackInfo;
            if (current == null || string.IsNullOrEmpty(current.Identifiers?.Uri) || !current.Player.IsPlaying)
                return (false, "Nenhuma música tocando no momento para pular.");

            string skippedTrackUri = current.Identifiers.Uri;
            string skippedTrackTitle = current.Media.Title;

            await SkipToNextAsync(cancellationToken);
            ClearVoteSkip(skippedTrackUri);

            Log($"Música '{skippedTrackTitle}' pulada manualmente.");
            return (true, $"Música '{skippedTrackTitle}' pulada!");
        }

        public async Task<VoteSkipResult> RegisterVoteSkipAsync(string userId, int requiredVotes, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(userId))
                return new VoteSkipResult(false, "Usuário inválido para votar.", 0, requiredVotes, false);

            if (requiredVotes <= 0)
                requiredVotes = 1;

            var current = CurrentTrackInfo;
            if (current == null || string.IsNullOrEmpty(current.Identifiers?.Uri) || !current.Player.IsPlaying)
                return new VoteSkipResult(false, "Nenhuma música tocando no momento para votar.", 0, requiredVotes, false);

            string trackUri = current.Identifiers.Uri;
            string trackTitle = current.Media.Title;

            var voters = _voteSkipTracker.GetOrAdd(trackUri, _ => new ConcurrentDictionary<string, byte>());

            if (!voters.TryAdd(userId, 0))
            {
                return new VoteSkipResult(
                    false,
                    $"Você já votou para pular '{trackTitle}'. ({voters.Count}/{requiredVotes} votos)",
                    voters.Count, requiredVotes, false);
            }

            int currentVotes = voters.Count;

            if (currentVotes >= requiredVotes)
            {
                await SkipToNextAsync(cancellationToken);
                ClearVoteSkip(trackUri);

                Log($"Música '{trackTitle}' pulada por votação ({currentVotes}/{requiredVotes}).");
                return new VoteSkipResult(true, $"Votação atingida! '{trackTitle}' foi pulada.", currentVotes, requiredVotes, true);
            }

            Log($"Voto de skip registrado para '{trackTitle}' ({currentVotes}/{requiredVotes}).");
            return new VoteSkipResult(
                true,
                $"Voto registrado! ({currentVotes}/{requiredVotes} votos para pular '{trackTitle}').",
                currentVotes, requiredVotes, false);
        }

        #endregion

        #region Public Methods - Playlist

        /// <summary>
        /// Adiciona a música tocando no momento à playlist informada (ex: playlist de lives).
        /// </summary>
        public async Task<(bool Success, string Message)> AddCurrentTrackToPlaylistAsync(
            string playlistId, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(playlistId))
                return (false, "Nenhuma playlist de lives configurada. Rode 'config' para escolher uma.");

            var current = CurrentTrackInfo;
            if (current == null || string.IsNullOrEmpty(current.Identifiers?.Uri) || !current.Player.IsPlaying)
                return (false, "Nenhuma música tocando no momento para adicionar à playlist.");

            try
            {
                var request = new PlaylistAddItemsRequest([current.Identifiers.Uri]);
                await _spotifyClient!.Playlists.AddPlaylistItems(playlistId, request, cancellationToken);

                Log($"Música '{current.Media.Title}' adicionada à playlist de lives ({playlistId}).");
                return (true, $"'{current.Media.Title}' foi adicionada à playlist de lives!");
            }
            catch (APIException ex) when (ex.Response?.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return (false, "Playlist de lives não encontrada. Verifique se ela ainda existe na sua conta.");
            }
            catch (APIException ex) when (ex.Response?.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return (false, "Sem permissão para adicionar músicas a essa playlist (ela precisa ser sua ou colaborativa).");
            }
            catch (Exception ex)
            {
                Log($"Erro ao adicionar música à playlist: {ex.Message}");
                return (false, "Não foi possível adicionar a música à playlist de lives.");
            }
        }

        #endregion

        #region Public Methods - Queue Management

        public async Task<List<SpotifyTrackInfo>> GetQueueAsync(int limit = 5, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            var finalQueue = new List<SpotifyTrackInfo>();

            lock (_userRequestedQueue)
            {
                finalQueue.AddRange(_userRequestedQueue.Take(limit));
            }

            if (finalQueue.Count >= limit)
            {
                return finalQueue;
            }

            var queueResponse = await _spotifyClient!.Player.GetQueue(cancellationToken);

            if (queueResponse?.Queue != null && queueResponse.Queue.Count > 0)
            {
                var existingUris = finalQueue
                    .Select(x => x.Identifiers?.Uri)
                    .Where(uri => !string.IsNullOrEmpty(uri))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrEmpty(CurrentTrackInfo?.Identifiers?.Uri))
                {
                    existingUris.Add(CurrentTrackInfo.Identifiers.Uri);
                }

                foreach (var item in queueResponse.Queue)
                {
                    if (item is FullTrack track)
                    {
                        if (!string.IsNullOrEmpty(track.Uri) && existingUris.Contains(track.Uri))
                            continue;

                        finalQueue.Add(MapToTrackInfo(track));

                        if (finalQueue.Count >= limit)
                            break;
                    }
                }
            }

            return finalQueue;
        }

        public async Task<SpotifyTrackInfo> AddToQueueAsync(string input, string userId, string userName, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("A entrada não pode estar vazia.", nameof(input));

            string trackUri;
            string searchKeyword = input.Trim();

            if (searchKeyword.StartsWith("spotify:track:", StringComparison.OrdinalIgnoreCase) ||
                searchKeyword.Contains("open.spotify.com/track/", StringComparison.OrdinalIgnoreCase) ||
                searchKeyword.Contains("open.spotify.com/intl-", StringComparison.OrdinalIgnoreCase))
            {
                trackUri = ExtractTrackUri(searchKeyword);
            }
            else if (searchKeyword.Contains("youtube.com/", StringComparison.OrdinalIgnoreCase) ||
                     searchKeyword.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase))
            {
                Log("Link do YouTube detectado. Obtendo título do vídeo...");
                string? videoTitle = await _youTubeService.GetVideoTitleAsync(searchKeyword, cancellationToken);

                if (string.IsNullOrWhiteSpace(videoTitle))
                    throw new Exception("Não foi possível obter o título do vídeo do YouTube.");

                Log($"Título do vídeo: \"{videoTitle}\". Buscando no Spotify...");
                trackUri = await SearchAndGetTrackUriAsync(videoTitle, cancellationToken);
            }
            else
            {
                Log($"Buscando no Spotify por: \"{searchKeyword}\"...");
                trackUri = await SearchAndGetTrackUriAsync(searchKeyword, cancellationToken);
            }

            if (string.IsNullOrEmpty(trackUri))
                throw new Exception("Nenhuma música correspondente foi encontrada no Spotify.");

            string trackId = trackUri.Replace("spotify:track:", "", StringComparison.OrdinalIgnoreCase).Trim();
            var trackDetails = await _spotifyClient!.Tracks.Get(trackId, cancellationToken)
                ?? throw new Exception("Não foi possível obter informações detalhadas da música.");

            var queueItem = MapToTrackInfo(trackDetails);
            queueItem.Request = new SpotifyRequestInfo
            {
                UserId = userId,
                UserName = userName,
                RequestedAt = DateTime.UtcNow
            };

            Log("---------------------------------------------");
            Log($"🔎 MÚSICA SELECIONADA POR @{queueItem.Request.UserName} ({queueItem.Request.UserId}):");
            Log($"   Nome: {queueItem.Media.Title}{(queueItem.Media.IsExplicit ? " [EXPLÍCITO]" : "")}");
            Log($"   Artista(s): {queueItem.Media.Artist}");
            Log($"   Álbum: {queueItem.Media.Album}");
            Log($"   Duração: {FormatDuration(queueItem.Player.DurationMs)}");
            Log("---------------------------------------------");

            await _playerLock.WaitAsync(cancellationToken);
            try
            {
                Log("Enviando comando AddToQueue para o Spotify...");
                await _spotifyClient.Player.AddToQueue(new PlayerAddToQueueRequest(trackUri), cancellationToken);
                Log("Comando AddToQueue aceito com sucesso!\n");

                lock (_userRequestedQueue)
                {
                    _userRequestedQueue.Add(queueItem);
                }
            }
            finally
            {
                _playerLock.Release();
            }

            return queueItem;
        }

        public Task<(bool Success, SpotifyTrackInfo? RemovedItem, string Message)> RemoveLastAddedFromQueueAsync(
            string userId,
            bool isModOrStreamer = false,
            CancellationToken cancellationToken = default)
        {
            lock (_userRequestedQueue)
            {
                var itemToRemove = _userRequestedQueue
                    .LastOrDefault(item => isModOrStreamer || item.Request?.UserId == userId);

                if (itemToRemove == null)
                    return Task.FromResult((false, (SpotifyTrackInfo?)null, "Você não possui nenhuma música pendente na fila para remover."));

                _userRequestedQueue.Remove(itemToRemove);
                if (!string.IsNullOrEmpty(itemToRemove.Identifiers?.Uri))
                    _canceledTrackUris.TryAdd(itemToRemove.Identifiers.Uri, 0);

                Log($"🗑️ A música '{itemToRemove.Media.Title}' pedida por @{itemToRemove.Request?.UserName} foi marcada para remoção/cancelamento.");

                return Task.FromResult((true, (SpotifyTrackInfo?)itemToRemove, $"A música '{itemToRemove.Media.Title}' foi removida com sucesso da sua fila!"));
            }
        }

        public List<SpotifyTrackInfo> GetPendingUserQueue()
        {
            lock (_userRequestedQueue)
                return [.. _userRequestedQueue];
        }

        #endregion

        #region Private Methods - Polling & Player State

        private async Task<(CurrentlyPlaying? Playing, FullTrack? Track)> FetchCurrentlyPlayingAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            try
            {
                var currentlyPlaying = await _spotifyClient!.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest(), cancellationToken);

                if (currentlyPlaying?.Item is FullTrack track && currentlyPlaying.IsPlaying)
                    return (currentlyPlaying, track);
            }
            catch (APIException ex) when (ex.Response?.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                ResetCurrentTrackInfo();
            }

            return (null, null);
        }

        private SpotifyTrackInfo UpdateCurrentTrackState(CurrentlyPlaying currentlyPlaying, FullTrack track)
        {
            string currentTrackUri = track.Uri;
            bool hasTrackChanged = false;
            SpotifyTrackInfo updatedTrack;

            if (currentTrackUri != _lastTrackUri)
            {
                hasTrackChanged = true;
                ClearVoteSkip(_lastTrackUri);
                _lastTrackUri = currentTrackUri;

                SpotifyTrackInfo? matchedRequest = null;
                lock (_userRequestedQueue)
                {
                    matchedRequest = _userRequestedQueue.FirstOrDefault(x => x.Identifiers?.Uri == currentTrackUri);
                    if (matchedRequest != null)
                        _userRequestedQueue.Remove(matchedRequest);
                }

                updatedTrack = MapToTrackInfo(track);
                updatedTrack.Player.IsPlaying = currentlyPlaying.IsPlaying;
                updatedTrack.Player.ProgressMs = currentlyPlaying.ProgressMs ?? 0;

                if (matchedRequest != null)
                    updatedTrack.Request = matchedRequest.Request;

                CurrentTrackInfo = updatedTrack;
            }
            else
            {
                CurrentTrackInfo.Player.ProgressMs = currentlyPlaying.ProgressMs ?? 0;
                CurrentTrackInfo.Player.DurationMs = track.DurationMs;
                CurrentTrackInfo.Player.IsPlaying = currentlyPlaying.IsPlaying;
                updatedTrack = CurrentTrackInfo;
            }

            if (hasTrackChanged)
            {
                OnTrackChanged?.Invoke(this, updatedTrack);
            }

            OnPlayerUpdated?.Invoke(this, updatedTrack);

            return updatedTrack;
        }

        private void ResetCurrentTrackInfo()
        {
            if (!string.IsNullOrEmpty(_lastTrackUri))
            {
                ClearVoteSkip(_lastTrackUri);
                _lastTrackUri = string.Empty;

                CurrentTrackInfo = new SpotifyTrackInfo
                {
                    Player = new SpotifyPlayerState { IsPlaying = false }
                };

                OnTrackChanged?.Invoke(this, CurrentTrackInfo);
                OnPlayerUpdated?.Invoke(this, CurrentTrackInfo);
            }
        }

        private void ClearVoteSkip(string trackUri)
        {
            if (!string.IsNullOrEmpty(trackUri))
                _voteSkipTracker.TryRemove(trackUri, out _);
        }

        #endregion

        #region Private Methods - Mapping

        private static SpotifyTrackInfo MapToTrackInfo(FullTrack track)
        {
            string trackUrl = string.Empty;
            if (track.ExternalUrls != null && track.ExternalUrls.TryGetValue("spotify", out string? url))
            {
                trackUrl = url ?? string.Empty;
            }

            string artists = track.Artists != null
                ? string.Join(", ", track.Artists.Select(a => a.Name))
                : string.Empty;

            return new SpotifyTrackInfo
            {
                Identifiers = new SpotifyIdentifiers
                {
                    Id = track.Id ?? string.Empty,
                    Uri = track.Uri ?? string.Empty,
                    Url = trackUrl
                },
                Media = new SpotifyMediaDetails
                {
                    Title = track.Name ?? string.Empty,
                    Artist = artists,
                    Album = track.Album?.Name ?? string.Empty,
                    AlbumArtUrl = track.Album?.Images?.FirstOrDefault()?.Url ?? string.Empty,
                    IsExplicit = track.Explicit
                },
                Player = new SpotifyPlayerState
                {
                    DurationMs = track.DurationMs,
                    IsPlaying = false
                }
            };
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _playerLock.Dispose();
            }

            _disposed = true;
        }

        #endregion

        #region Private Methods - Helpers

        private static string ExtractTrackUri(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            string trimmed = input.Trim();

            if (trimmed.StartsWith("spotify:track:", StringComparison.OrdinalIgnoreCase))
            {
                string idOnly = trimmed.Replace("spotify:track:", "", StringComparison.OrdinalIgnoreCase)
                                       .Split('?')[0].Trim();
                return $"spotify:track:{idOnly}";
            }

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri))
            {
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                int trackIndex = Array.IndexOf(segments, "track");

                if (trackIndex >= 0 && trackIndex + 1 < segments.Length)
                {
                    string trackId = segments[trackIndex + 1].Split('?')[0].Trim();
                    if (!string.IsNullOrEmpty(trackId))
                        return $"spotify:track:{trackId}";
                }
            }

            return string.Empty;
        }

        private async Task<string> SearchAndGetTrackUriAsync(string query, CancellationToken cancellationToken = default)
        {
            var searchRequest = new SearchRequest(SearchRequest.Types.Track, query)
            {
                Limit = 1
            };

            var searchResult = await _spotifyClient!.Search.Item(searchRequest, cancellationToken);

            if (searchResult?.Tracks?.Items != null && searchResult.Tracks.Items.Count > 0)
            {
                var topTrack = searchResult.Tracks.Items[0];
                return topTrack.Uri;
            }

            return string.Empty;
        }

        private static string GenerateProgressBar(long progressMs, long durationMs, int totalBlocks = 20)
        {
            if (durationMs <= 0)
                return new string('░', totalBlocks) + " 00:00 / 00:00";

            double percent = Math.Clamp((double)progressMs / durationMs, 0.0, 1.0);
            int filledBlocks = (int)Math.Round(percent * totalBlocks);
            int emptyBlocks = totalBlocks - filledBlocks;

            string bar = new string('█', filledBlocks) + new string('░', emptyBlocks);

            return $"[{bar}] {FormatDuration(progressMs)} / {FormatDuration(durationMs)} ({percent * 100:F0}%)";
        }

        private static string FormatDuration(long durationMs)
        {
            TimeSpan time = TimeSpan.FromMilliseconds(durationMs);
            return time.TotalHours >= 1
                ? time.ToString(@"hh\:mm\:ss")
                : time.ToString(@"mm\:ss");
        }

        private void EnsureInitialized()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_spotifyClient == null)
                throw new InvalidOperationException("O SpotifyClient não foi inicializado no SpotifyService. Utilize o SpotifyAuthService para autenticar e chame SetClient().");
        }

        private static void Log(string message)
        {
            Console.WriteLine($"[SpotifyService] {message}");
        }

        #endregion
    }
}