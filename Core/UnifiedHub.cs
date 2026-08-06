// Core\UnifiedHub.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StreamerBot.UnifiedHub.Core.Models;
using StreamerBot.UnifiedHub.Core.Services;
using StreamerBot.UnifiedHub.Integrations;

namespace StreamerBot.UnifiedHub.Core
{
    public class UnifiedHub
    {
        private readonly ConfigManager _configManager;
        private readonly SpotifyConfig _config;

        public SpotifyService Spotify { get; private set; }
        public OAuthListener OAuth { get; private set; }

        public UnifiedHub()
        {
            _configManager = new ConfigManager();
            _config = _configManager.LoadConfig();

            Spotify = new SpotifyService(_config);
            OAuth = new OAuthListener();
        }

        #region Inicialização e Autenticação

        public async Task InitializeAsync()
        {
            Console.WriteLine("[SpotifyHub] Inicializando serviço...");

            // 1. Tenta autenticar em segundo plano via Refresh Token se houver credenciais salvas
            bool autenticado = await Spotify.InitializeAsync();
            if (autenticado)
            {
                Console.WriteLine("[SpotifyHub] Autenticado com sucesso via Refresh Token!");
                return;
            }

            // 2. Abre a interface Web e só avança após o fluxo completo e validação do token
            var (clientId, clientSecret, refreshToken) = await OAuth.ExecutarFluxoAutenticacaoEValidarAsync(
                _config.RedirectUri,
                _config,
                Spotify
            );

            if (!string.IsNullOrEmpty(refreshToken))
            {
                _config.ClientId = clientId;
                _config.ClientSecret = clientSecret;
                _config.RefreshToken = refreshToken;
                _configManager.SaveConfig(_config);

                Spotify.UpdateConfig(_config);
                await Spotify.InitializeAsync();
                Console.WriteLine("[SpotifyHub] Spotify configurado e autenticado com sucesso!");
            }
            else
            {
                Console.WriteLine("[SpotifyHub] Falha na autenticação do Spotify.");
            }
        }

        #endregion

        #region Leitura de Informações (Display & Links)

        /// <summary>
        /// Display Current Spotify Song
        /// </summary>
        public async Task<string> ObterMusicaAtualAsync()
        {
            return await Spotify.GetCurrentlyPlayingAsync();
        }

        /// <summary>
        /// Display Current Spotify Song Link
        /// </summary>
        public async Task<string> ObterLinkMusicaAtualAsync()
        {
            return await Spotify.GetCurrentlyPlayingLinkAsync();
        }

        /// <summary>
        /// Display Last Played Song
        /// </summary>
        public async Task<string> ObterUltimaMusicaTocadaAsync()
        {
            return await Spotify.GetLastPlayedSongAsync();
        }

        /// <summary>
        /// Get next X songs / Display Spotify Request Queue
        /// </summary>
        public async Task<List<string>> ObterFilaReproducaoAsync(int quantidade = 5)
        {
            return await Spotify.GetQueueAsync(quantidade);
        }

        /// <summary>
        /// View and Select Playlists
        /// </summary>
        public async Task<List<(string Id, string Name)>> ObterPlaylistsDoUsuarioAsync()
        {
            return await Spotify.GetUserPlaylistsAsync();
        }

        #endregion

        #region Controle do Player (Play, Pause, Skip, Prev, Seek)

        /// <summary>
        /// Alterna entre Play e Pause dependendo do estado atual
        /// </summary>
        public async Task AlternarPlayPauseAsync()
        {
            bool estaTocando = await Spotify.IsPlayingAsync();

            if (estaTocando)
            {
                await Spotify.PausePlaybackAsync();
            }
            else
            {
                await Spotify.ResumePlaybackAsync();
            }
        }

        /// <summary>
        /// Resume Spotify Player
        /// </summary>
        public async Task RetomarPlayerAsync()
        {
            await Spotify.ResumePlaybackAsync();
        }

        /// <summary>
        /// Pause Spotify Player
        /// </summary>
        public async Task PausarPlayerAsync()
        {
            await Spotify.PausePlaybackAsync();
        }

        /// <summary>
        /// Skip Spotify Song
        /// </summary>
        public async Task ProximaMusicaAsync()
        {
            await Spotify.SkipNextAsync();
        }

        /// <summary>
        /// Play Previous Spotify Song
        /// </summary>
        public async Task MusicaAnteriorAsync()
        {
            await Spotify.SkipPreviousAsync();
        }

        /// <summary>
        /// Restart Current Spotify Song
        /// </summary>
        public async Task ReiniciarMusicaAtualAsync()
        {
            await Spotify.RestartCurrentSongAsync();
        }

        #endregion

        #region Fila de Pedidos e Ações de Moderação

        /// <summary>
        /// Send Spotify Song Request
        /// </summary>
        public async Task<bool> PedirMusicaAsync(string trackUriOuUrl)
        {
            if (string.IsNullOrWhiteSpace(trackUriOuUrl)) return false;
            return await Spotify.SendSongRequestAsync(trackUriOuUrl);
        }

        /// <summary>
        /// Remove last Song Request
        /// </summary>
        public async Task<bool> RemoverUltimoPedidoAsync()
        {
            return await Spotify.RemoveLastSongRequestAsync();
        }

        #endregion

        #region Gerenciamento de Playlists

        /// <summary>
        /// Play Selected Playlist
        /// </summary>
        public async Task TocarPlaylistAsync(string playlistIdOuUri)
        {
            if (string.IsNullOrWhiteSpace(playlistIdOuUri)) return;
            await Spotify.PlayPlaylistAsync(playlistIdOuUri);
        }

        /// <summary>
        /// Add Song to Spotify Playlist
        /// </summary>
        public async Task<bool> AdicionarMusicaAPlaylistAsync(string playlistId, string trackUriOuUrl = null)
        {
            if (string.IsNullOrWhiteSpace(playlistId)) return false;
            return await Spotify.AddSongToPlaylistAsync(playlistId, trackUriOuUrl);
        }

        /// <summary>
        /// Adiciona a música tocando no momento à playlist em execução atual.
        /// </summary>
        public async Task<bool> AdicionarMusicaAtualAPlaylistAtualAsync()
        {
            return await Spotify.AddCurrentTrackToCurrentPlaylistAsync();
        }

        #endregion
    }
}