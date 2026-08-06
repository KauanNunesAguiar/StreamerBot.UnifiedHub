// Integrations\SpotifyService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Integrations
{
    public class SpotifyService
    {
        private SpotifyConfig _config;
        private SpotifyClient _spotify;

        public SpotifyService(SpotifyConfig config)
        {
            _config = config ?? new SpotifyConfig();
        }

        public void UpdateConfig(SpotifyConfig config)
        {
            _config = config;
        }

        public async Task<bool> InitializeAsync()
        {
            if (string.IsNullOrEmpty(_config.RefreshToken) ||
                string.IsNullOrEmpty(_config.ClientId) ||
                string.IsNullOrEmpty(_config.ClientSecret))
            {
                return false;
            }

            try
            {
                var response = await new OAuthClient().RequestToken(
                    new AuthorizationCodeRefreshRequest(_config.ClientId, _config.ClientSecret, _config.RefreshToken)
                );

                var spotifyConfig = SpotifyClientConfig.CreateDefault().WithToken(response.AccessToken);
                _spotify = new SpotifyClient(spotifyConfig);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Spotify] Falha ao inicializar com RefreshToken: {ex.Message}");
                return false;
            }
        }

        public async Task<string> ExchangeCodeForRefreshTokenAsync(string clientId, string clientSecret, string code, string redirectUri)
        {
            if (string.IsNullOrEmpty(code)) return string.Empty;

            var response = await new OAuthClient().RequestToken(
                new AuthorizationCodeTokenRequest(
                    clientId,
                    clientSecret,
                    code,
                    new Uri(redirectUri)
                )
            );

            return response.RefreshToken;
        }

        #region Reprodução Básica

        public async Task<string> GetCurrentlyPlayingAsync()
        {
            if (_spotify == null) return "Spotify não autenticado";

            try
            {
                var item = await _spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
                if (item?.Item is FullTrack track)
                {
                    string artistas = string.Join(", ", track.Artists.Select(a => a.Name));
                    return $"{track.Name} - {artistas}";
                }

                return "Nenhuma música tocando no momento.";
            }
            catch (Exception ex)
            {
                return $"Erro ao obter música: {ex.Message}";
            }
        }

        public async Task<string> GetCurrentlyPlayingLinkAsync()
        {
            if (_spotify == null) return string.Empty;

            try
            {
                var item = await _spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
                if (item?.Item is FullTrack track)
                {
                    return track.ExternalUrls.ContainsKey("spotify")
                        ? track.ExternalUrls["spotify"]
                        : string.Empty;
                }
            }
            catch { }

            return string.Empty;
        }

        // Checa de fato se está reproduzindo (IsPlaying) para alternar corretamente
        public async Task<bool> IsPlayingAsync()
        {
            if (_spotify == null) return false;
            try
            {
                var currentlyPlaying = await _spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
                return currentlyPlaying != null && currentlyPlaying.IsPlaying;
            }
            catch
            {
                return false;
            }
        }

        public async Task PausePlaybackAsync()
        {
            if (_spotify == null) return;
            try { await _spotify.Player.PausePlayback(); } catch { }
        }

        public async Task ResumePlaybackAsync()
        {
            if (_spotify == null) return;
            try { await _spotify.Player.ResumePlayback(); } catch { }
        }

        public async Task SkipNextAsync()
        {
            if (_spotify == null) return;
            try { await _spotify.Player.SkipNext(); } catch { }
        }

        public async Task SkipPreviousAsync()
        {
            if (_spotify == null) return;
            try { await _spotify.Player.SkipPrevious(); } catch { }
        }

        public async Task RestartCurrentSongAsync()
        {
            if (_spotify == null) return;
            try { await _spotify.Player.SeekTo(new PlayerSeekToRequest(0)); } catch { }
        }

        public async Task<string> GetLastPlayedSongAsync()
        {
            if (_spotify == null) return "Spotify não autenticado";

            try
            {
                var history = await _spotify.Player.GetRecentlyPlayed(new PlayerRecentlyPlayedRequest { Limit = 1 });
                var lastItem = history?.Items?.FirstOrDefault();

                if (lastItem != null)
                {
                    string artistas = string.Join(", ", lastItem.Track.Artists.Select(a => a.Name));
                    return $"{lastItem.Track.Name} - {artistas}";
                }

                return "Nenhum histórico recente encontrado.";
            }
            catch (Exception ex)
            {
                return $"Erro ao buscar histórico: {ex.Message}";
            }
        }

        #endregion

        #region Fila de Reprodução & Requests

        /// <summary>
        /// Aceita URI, Link Web ou Nome da Música (+ Artista) para adicionar à fila.
        /// </summary>
        public async Task<bool> SendSongRequestAsync(string input)
        {
            if (_spotify == null || string.IsNullOrWhiteSpace(input)) return false;

            try
            {
                string uri = await ResolvaOuBusqueTrackUriAsync(input);
                if (string.IsNullOrEmpty(uri))
                {
                    Console.WriteLine("[Spotify] Nenhuma música encontrada.");
                    return false;
                }

                await _spotify.Player.AddToQueue(new PlayerAddToQueueRequest(uri));
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Spotify Error - SendSongRequest]: {ex.Message}");
                return false;
            }
        }

        public async Task<List<string>> GetQueueAsync(int limit = 5)
        {
            var resultado = new List<string>();
            if (_spotify == null) return resultado;

            try
            {
                var queue = await _spotify.Player.GetQueue();
                if (queue?.Queue != null)
                {
                    foreach (var item in queue.Queue.Take(limit))
                    {
                        if (item is FullTrack track)
                        {
                            string artistas = string.Join(", ", track.Artists.Select(a => a.Name));
                            resultado.Add($"{track.Name} - {artistas}");
                        }
                    }
                }
            }
            catch { }

            return resultado;
        }

        public async Task<bool> RemoveLastSongRequestAsync()
        {
            if (_spotify == null) return false;
            try
            {
                return await _spotify.Player.SkipNext();
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Playlists

        public async Task<List<(string Id, string Name)>> GetUserPlaylistsAsync()
        {
            var result = new List<(string Id, string Name)>();
            if (_spotify == null) return result;

            try
            {
                var playlists = await _spotify.Playlists.CurrentUsers();
                if (playlists?.Items != null)
                {
                    foreach (var pl in playlists.Items)
                    {
                        result.Add((pl.Id, pl.Name));
                    }
                }
            }
            catch { }

            return result;
        }

        public async Task PlayPlaylistAsync(string playlistIdOrUri)
        {
            if (_spotify == null) return;

            try
            {
                string contextUri = playlistIdOrUri.StartsWith("spotify:playlist:")
                    ? playlistIdOrUri
                    : $"spotify:playlist:{playlistIdOrUri}";

                await _spotify.Player.ResumePlayback(new PlayerResumePlaybackRequest
                {
                    ContextUri = contextUri
                });
            }
            catch { }
        }

        public async Task<bool> AddSongToPlaylistAsync(string playlistInput, string trackInput = null)
        {
            if (_spotify == null || string.IsNullOrWhiteSpace(playlistInput)) return false;

            try
            {
                string targetTrackUri = string.Empty;

                // 1. Resolve a música (Se vazia, pega a que está tocando agora)
                if (string.IsNullOrWhiteSpace(trackInput))
                {
                    var current = await _spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());
                    if (current?.Item is FullTrack track)
                    {
                        targetTrackUri = track.Uri;
                    }
                }
                else
                {
                    targetTrackUri = await ResolvaOuBusqueTrackUriAsync(trackInput);
                }

                if (string.IsNullOrEmpty(targetTrackUri))
                {
                    Console.WriteLine("[Spotify] Não foi possível identificar a música para adicionar.");
                    return false;
                }

                // 2. Extrai ID limpo da Playlist
                string cleanPlaylistId = ExtrairPlaylistId(playlistInput);

                // 3. Adiciona na playlist
                var request = new PlaylistAddItemsRequest(new List<string> { targetTrackUri });
                var response = await _spotify.Playlists.AddPlaylistItems(cleanPlaylistId, request);

                return !string.IsNullOrEmpty(response?.SnapshotId);
            }
            catch (APIException apiEx)
            {
                Console.WriteLine($"[Spotify Error - AddToPlaylist API]: Status {apiEx.Response?.StatusCode} - {apiEx.Message}");
                if (apiEx.Response?.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Console.WriteLine("[Dica 403 Forbidden]: Certifique-se de que a playlist pertence à SUA conta e que você re-autenticou com o escopo 'playlist-modify-public/private'.");
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Spotify Error - AddToPlaylist]: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Adiciona a música tocando no momento à playlist em reprodução atual.
        /// </summary>
        public async Task<bool> AddCurrentTrackToCurrentPlaylistAsync()
        {
            if (_spotify == null) return false;

            try
            {
                // 1. Obtém o contexto atual de reprodução
                var currentlyPlaying = await _spotify.Player.GetCurrentlyPlaying(new PlayerCurrentlyPlayingRequest());

                if (currentlyPlaying == null || currentlyPlaying.Context == null)
                {
                    Console.WriteLine("[Spotify Error]: Nenhuma mídia ou contexto em reprodução.");
                    return false;
                }

                // 2. Verifica se o contexto atual é de fato uma Playlist
                if (currentlyPlaying.Context.Type != "playlist")
                {
                    Console.WriteLine("[Spotify Error]: O player não está tocando uma playlist no momento.");
                    return false;
                }

                // Extrai o ID da playlist do Context.Uri (spotify:playlist:XXXXX)
                string playlistUri = currentlyPlaying.Context.Uri;
                string playlistId = ExtrairPlaylistId(playlistUri);

                // 3. Obtém a faixa atual
                if (currentlyPlaying.Item is not FullTrack currentTrack)
                {
                    Console.WriteLine("[Spotify Error]: Nenhuma faixa/música identificada no player.");
                    return false;
                }

                // 4. Adiciona a música atual à playlist em reprodução
                var request = new PlaylistAddItemsRequest(new List<string> { currentTrack.Uri });
                var response = await _spotify.Playlists.AddPlaylistItems(playlistId, request);

                return !string.IsNullOrEmpty(response?.SnapshotId);
            }
            catch (APIException apiEx)
            {
                Console.WriteLine($"[Spotify Error - AddToPlaylist API]: Status {apiEx.Response?.StatusCode} - {apiEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Spotify Error - AddToPlaylist]: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Auxiliares

        private async Task<string> ResolvaOuBusqueTrackUriAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            string trimmed = input.Trim();

            // 1. Se já for uma URI direta (ex: spotify:track:4iV5W9uYEdYUVa79Axb7Rh)
            if (trimmed.StartsWith("spotify:track:", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            // 2. Se for um link web do navegador (ex: https://open.spotify.com/track/4iV5W9uYEdYUVa79Axb7Rh?si=...)
            if (trimmed.Contains("spotify.com/track/") || trimmed.Contains("open.spotify.com/"))
            {
                string id = ExtrairTrackId(trimmed);
                if (!string.IsNullOrEmpty(id))
                {
                    return $"spotify:track:{id}";
                }
            }

            // 3. Se for texto comum (Nome da música + Artista), faz a busca na API
            try
            {
                var searchRequest = new SearchRequest(SearchRequest.Types.Track, trimmed)
                {
                    Limit = 1
                };

                var searchResult = await _spotify.Search.Item(searchRequest);
                var firstTrack = searchResult.Tracks?.Items?.FirstOrDefault();

                if (firstTrack != null)
                {
                    string artistas = string.Join(", ", firstTrack.Artists.Select(a => a.Name));
                    Console.WriteLine($"[Spotify Search] Encontrado: '{firstTrack.Name} - {artistas}' ({firstTrack.Uri})");
                    return firstTrack.Uri;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Spotify Search Error]: {ex.Message}");
            }

            return string.Empty;
        }

        private string ExtrairTrackId(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            try
            {
                // Se for um link HTTP completo
                if (input.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new Uri(input);
                    // Exemplo de Path: /track/4iV5W9uYEdYUVa79Axb7Rh
                    var segments = uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    int trackIndex = Array.IndexOf(segments, "track");
                    if (trackIndex >= 0 && trackIndex + 1 < segments.Length)
                    {
                        return segments[trackIndex + 1].Split('?').FirstOrDefault()?.Trim();
                    }
                }

                // Se for URI do formato spotify:track:ID
                if (input.Contains("spotify:track:"))
                {
                    return input.Replace("spotify:track:", "").Split('?').FirstOrDefault()?.Trim();
                }
            }
            catch
            {
                // Fallback caso ocorra erro no parsing da URI
            }

            // Fallback genérico para pegar o último segmento limpo
            return input.Split('/').LastOrDefault()?.Split('?').FirstOrDefault()?.Trim();
        }

        private string ExtrairPlaylistId(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            if (input.Contains("open.spotify.com/playlist/"))
            {
                var uri = new Uri(input);
                string path = uri.AbsolutePath.Split(new[] { "/playlist/" }, StringSplitOptions.None).LastOrDefault();
                return path?.Split('?').FirstOrDefault()?.Trim();
            }

            return input.Replace("spotify:playlist:", "").Split('?').FirstOrDefault()?.Trim();
        }

        #endregion
    }
}