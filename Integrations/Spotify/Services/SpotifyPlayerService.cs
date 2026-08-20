using System.Collections.Concurrent;
using System.Net.Http.Json;
using SpotifyAPI.Web;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyPlayerService
    {
        #region Fields

        private static readonly HttpClient HttpClient = new();
        private SpotifyClient? _spotifyClient;
        private string _lastTrackUri = string.Empty;
        private readonly ConcurrentDictionary<string, byte> _canceledTrackUris = new();
        private readonly List<SpotifyTrackInfo> _userRequestedQueue = [];
        private readonly SemaphoreSlim _playerLock = new(1, 1);

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
                    await _playerLock.WaitAsync(cancellationToken);
                    try
                    {
                        var (currentlyPlaying, track) = await FetchCurrentlyPlayingAsync(cancellationToken);

                        if (currentlyPlaying != null && track != null)
                        {
                            UpdateCurrentTrackState(currentlyPlaying, track);

                            if (_canceledTrackUris.ContainsKey(track.Uri))
                            {
                                Log($"[UNDO AUTO] A música '{track.Name}' foi desfeita. Pulando automaticamente...");
                                _canceledTrackUris.TryRemove(track.Uri, out _);

                                await SkipToNextAsync();

                                lock (_userRequestedQueue)
                                {
                                    _userRequestedQueue.RemoveAll(x => x.Identifiers.Uri == track.Uri);
                                }

                                await Task.Delay(1000, cancellationToken);
                                continue;
                            }

                            string progressBar = GenerateProgressBar(CurrentTrackInfo.Player.ProgressMs, CurrentTrackInfo.Player.DurationMs);
                            Log($"Tocando agora: {track.Name} - {progressBar}");
                        }
                    }
                    finally
                    {
                        _playerLock.Release();
                    }

                    // Aguarda o tempo normal entre as requisições
                    await Task.Delay(intervalMilliseconds, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Ocorreu quando a aplicação solicitou encerramento. Sai suavemente do loop.
                    Log("Monitoramento de músicas encerrado com sucesso.");
                    break;
                }
                catch (APIException ex) when ((int?)ex.Response?.StatusCode == 429) // TooManyRequests
                {
                    // O Spotify informa quanto tempo você deve esperar antes de tentar novamente.
                    // Padrão de 30 segundos de cooldown se a resposta não trouxer o tempo exato.
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

        public async Task PauseAsync()
        {
            EnsureInitialized();
            await _spotifyClient!.Player.PausePlayback();
            CurrentTrackInfo.Player.IsPlaying = false;
            OnPlayerUpdated?.Invoke(this, CurrentTrackInfo);
        }

        public async Task ResumeAsync()
        {
            EnsureInitialized();
            await _spotifyClient!.Player.ResumePlayback();
            CurrentTrackInfo.Player.IsPlaying = true;
            OnPlayerUpdated?.Invoke(this, CurrentTrackInfo);
        }

        public async Task SkipToNextAsync()
        {
            EnsureInitialized();
            await _spotifyClient!.Player.SkipNext();
        }

        public async Task SkipToPreviousAsync()
        {
            EnsureInitialized();
            await _spotifyClient!.Player.SkipPrevious();
        }

        public async Task SetVolumeAsync(int volumePercent)
        {
            EnsureInitialized();
            int volume = Math.Clamp(volumePercent, 0, 100);
            await _spotifyClient!.Player.SetVolume(new PlayerVolumeRequest(volume));
        }

        public string GetCurrentTrackProgressBar()
        {
            if (CurrentTrackInfo == null || CurrentTrackInfo.Player.DurationMs <= 0)
                return "Nenhuma música tocando no momento.";

            return GenerateProgressBar(CurrentTrackInfo.Player.ProgressMs, CurrentTrackInfo.Player.DurationMs);
        }

        #endregion

        #region Public Methods - Queue Management

        public async Task<List<SpotifyTrackInfo>> GetQueueAsync(int limit = 5)
        {
            EnsureInitialized();

            var queueResponse = await _spotifyClient!.Player.GetQueue();
            var upcomingTracks = new List<SpotifyTrackInfo>();

            if (queueResponse?.Queue == null) return upcomingTracks;

            foreach (var item in queueResponse.Queue.Take(limit))
            {
                if (item is FullTrack track)
                    upcomingTracks.Add(MapToTrackInfo(track));
            }

            return upcomingTracks;
        }

        public async Task<SpotifyTrackInfo> AddToQueueAsync(string input, string userId, string userName)
        {
            EnsureInitialized();

            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("A entrada não pode estar vazia.", nameof(input));

            string trackUri = string.Empty;
            string searchKeyword = input.Trim();

            // 1. Identificação do tipo de entrada (Spotify URI/URL, YouTube ou Busca por Nome)
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
                string? videoTitle = await GetYouTubeVideoTitleAsync(searchKeyword);

                if (string.IsNullOrWhiteSpace(videoTitle))
                    throw new Exception("Não foi possível obter o título do vídeo do YouTube.");

                Log($"Título do vídeo: \"{videoTitle}\". Buscando no Spotify...");
                trackUri = await SearchAndGetTrackUriAsync(videoTitle);
            }
            else
            {
                Log($"Buscando no Spotify por: \"{searchKeyword}\"...");
                trackUri = await SearchAndGetTrackUriAsync(searchKeyword);
            }

            if (string.IsNullOrEmpty(trackUri))
                throw new Exception("Nenhuma música correspondente foi encontrada no Spotify.");

            // 2. Consulta os detalhes da faixa na API
            string trackId = trackUri.Replace("spotify:track:", "", StringComparison.OrdinalIgnoreCase).Trim();
            var trackDetails = await _spotifyClient!.Tracks.Get(trackId)
                ?? throw new Exception("Não foi possível obter informações detalhadas da música.");

            // 3. Mapeia para o modelo unificado SpotifyTrackInfo
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

            await _playerLock.WaitAsync();
            try
            {
                Log("Enviando comando AddToQueue para o Spotify...");
                await _spotifyClient.Player.AddToQueue(new PlayerAddToQueueRequest(trackUri));
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

        public Task<(bool Success, SpotifyTrackInfo? RemovedItem, string Message)> RemoveLastAddedFromQueueAsync(string userId, bool isModOrStreamer = false)
        {
            lock (_userRequestedQueue)
            {
                var itemToRemove = _userRequestedQueue
                    .LastOrDefault(item => isModOrStreamer || item.Request.UserId == userId);

                if (itemToRemove == null)
                    return Task.FromResult((false, (SpotifyTrackInfo?)null, "Você não possui nenhuma música pendente na fila para remover."));

                _userRequestedQueue.Remove(itemToRemove);
                if (!string.IsNullOrEmpty(itemToRemove.Identifiers.Uri))
                    _canceledTrackUris.TryAdd(itemToRemove.Identifiers.Uri, 0);

                Log($"🗑️ A música '{itemToRemove.Media.Title}' pedida por @{itemToRemove.Request.UserName} foi marcada para remoção/cancelamento.");

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

            if (currentTrackUri != _lastTrackUri)
            {
                _lastTrackUri = currentTrackUri;
                SpotifyTrackInfo? matchedRequest = null;

                lock (_userRequestedQueue)
                {
                    matchedRequest = _userRequestedQueue.FirstOrDefault(x => x.Identifiers.Uri == currentTrackUri);
                    if (matchedRequest != null)
                        _userRequestedQueue.Remove(matchedRequest);
                }

                CurrentTrackInfo = MapToTrackInfo(track);
                CurrentTrackInfo.Player.IsPlaying = currentlyPlaying.IsPlaying;
                CurrentTrackInfo.Player.ProgressMs = currentlyPlaying.ProgressMs ?? 0;

                if (matchedRequest != null)
                    CurrentTrackInfo.Request = matchedRequest.Request;

                OnTrackChanged?.Invoke(this, CurrentTrackInfo);
                OnPlayerUpdated?.Invoke(this, CurrentTrackInfo);
            }
            else
            {
                CurrentTrackInfo.Player.ProgressMs = currentlyPlaying.ProgressMs ?? 0;
                CurrentTrackInfo.Player.DurationMs = track.DurationMs;
                CurrentTrackInfo.Player.IsPlaying = currentlyPlaying.IsPlaying;

                OnPlayerUpdated?.Invoke(this, CurrentTrackInfo);
            }

            return CurrentTrackInfo;
        }

        private void ResetCurrentTrackInfo()
        {
            if (!string.IsNullOrEmpty(_lastTrackUri))
            {
                _lastTrackUri = string.Empty;

                CurrentTrackInfo = new SpotifyTrackInfo
                {
                    Player = new SpotifyPlayerState { IsPlaying = false }
                };

                OnTrackChanged?.Invoke(this, CurrentTrackInfo);
                OnPlayerUpdated?.Invoke(this, CurrentTrackInfo);
            }
        }

        #endregion

        #region Private Methods - Mapping

        private static SpotifyTrackInfo MapToTrackInfo(FullTrack track)
        {
            track.ExternalUrls.TryGetValue("spotify", out string? trackUrl);

            return new SpotifyTrackInfo
            {
                Identifiers = new SpotifyIdentifiers
                {
                    Id = track.Id ?? string.Empty,
                    Uri = track.Uri ?? string.Empty,
                    Url = trackUrl ?? string.Empty
                },
                Media = new SpotifyMediaDetails
                {
                    Title = track.Name,
                    Artist = string.Join(", ", track.Artists.Select(a => a.Name)),
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

        private async Task<string> SearchAndGetTrackUriAsync(string query)
        {
            var searchRequest = new SearchRequest(SearchRequest.Types.Track, query)
            {
                Limit = 1
            };

            var searchResult = await _spotifyClient!.Search.Item(searchRequest);

            if (searchResult?.Tracks?.Items != null && searchResult.Tracks.Items.Count > 0)
            {
                var topTrack = searchResult.Tracks.Items[0];
                return topTrack.Uri;
            }

            return string.Empty;
        }

        private static async Task<string?> GetYouTubeVideoTitleAsync(string youtubeUrl)
        {
            try
            {
                string oembedUrl = $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(youtubeUrl)}&format=json";
                var response = await HttpClient.GetFromJsonAsync<YouTubeOEmbedResponse>(oembedUrl);
                return response?.Title;
            }
            catch
            {
                return null;
            }
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
            if (_spotifyClient == null)
                throw new InvalidOperationException("O SpotifyClient não foi inicializado no SpotifyService. Utilize o SpotifyAuthService para autenticar e chame SetClient().");
        }

        private static void Log(string message)
        {
            Console.WriteLine($"[SpotifyService] {message}");
        }

        #endregion

        #region Nested Types

        private record YouTubeOEmbedResponse(string Title);

        #endregion
    }
}