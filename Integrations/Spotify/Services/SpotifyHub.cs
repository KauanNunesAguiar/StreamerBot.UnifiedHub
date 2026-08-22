using Newtonsoft.Json;
using StreamerBot.UnifiedHub.Core.Models;
using StreamerBot.UnifiedHub.Core.Services;
using StreamerBot.UnifiedHub.Integrations.Spotify.Extensions;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;
using StreamerBot.UnifiedHub.Integrations.Youtube.Services;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public static class SpotifyHub
    {
        private static SpotifyManager? _manager;
        private static CancellationTokenSource? _pollingCts;
        private static readonly SemaphoreSlim _initLock = new(1, 1);
        private static volatile bool _isInitialized;

        public static bool IsInitialized => _isInitialized;

        public static event EventHandler<SpotifyTrackInfo>? OnTrackChanged;
        public static event EventHandler<SpotifyTrackInfo>? OnPlayerUpdated;

        #region Inicialização

        /// <summary>
        /// Inicializa toda a integração: carrega configurações salvas, autentica com o Spotify
        /// (abrindo o navegador automaticamente se necessário) e inicia o monitoramento contínuo
        /// da música tocando. Idempotente - seguro chamar múltiplas vezes.
        /// </summary>
        public static async Task<HubResult> InitializeAsync(int pollingIntervalMs = 5000, CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
                return HubResult.Ok("Spotify já estava inicializado.");

            await _initLock.WaitAsync(cancellationToken);
            try
            {
                if (_isInitialized)
                    return HubResult.Ok("Spotify já estava inicializado.");

                var configManager = new ConfigManager();
                var appConfig = configManager.Load();
                var spotifyConfig = appConfig.GetSpotifyConfig();

                var httpServer = new LocalHttpListener();
                var browserService = new SystemBrowser();
                var oauthHandler = new SpotifyOAuthHandler(httpServer, browserService, configManager);

                var youTubeService = new YouTubeService(SharedHttpClient.Instance);
                var playerService = new SpotifyPlayerService(youTubeService);

                var manager = new SpotifyManager(oauthHandler, playerService, spotifyConfig, configManager);

                manager.OnTrackChanged += (sender, track) => OnTrackChanged?.Invoke(sender, track);
                manager.OnPlayerUpdated += (sender, track) => OnPlayerUpdated?.Invoke(sender, track);

                await manager.InitializeAsync(cancellationToken);

                _manager = manager;

                _pollingCts = new CancellationTokenSource();
                _ = manager.StartPollingAsync(pollingIntervalMs, _pollingCts.Token); // background, não bloqueia

                _isInitialized = true;
                return HubResult.Ok("Spotify inicializado e conectado com sucesso.");
            }
            catch (Exception ex)
            {
                return HubResult.Fail(BuildFriendlyError(ex, "inicializar o Spotify"));
            }
            finally
            {
                _initLock.Release();
            }
        }

        /// <summary>Refaz o fluxo de autenticação/configuração (ex: trocar de conta ou playlist).</summary>
        public static Task<HubResult> ReconfigureAsync(CancellationToken cancellationToken = default)
            => ExecuteAsync(
                async () => { await _manager!.ReconfigureAsync(cancellationToken); },
                "Reconfiguração concluída com sucesso.",
                "reconfigurar o Spotify");

        #endregion

        #region Player

        public static Task<HubResult> PauseAsync(CancellationToken cancellationToken = default)
            => ExecuteAsync(
                async () => { await _manager!.PauseAsync(cancellationToken); },
                "Reprodução pausada.",
                "pausar a música");

        public static Task<HubResult> ResumeAsync(CancellationToken cancellationToken = default)
            => ExecuteAsync(
                async () => { await _manager!.ResumeAsync(cancellationToken); },
                "Reprodução retomada.",
                "retomar a música");

        public static Task<HubResult> NextAsync(CancellationToken cancellationToken = default)
            => ExecuteAsync(
                async () => { await _manager!.SkipToNextAsync(cancellationToken); },
                "Música pulada para a próxima.",
                "pular para a próxima música");

        public static Task<HubResult> PreviousAsync(CancellationToken cancellationToken = default)
            => ExecuteAsync(
                async () => { await _manager!.SkipToPreviousAsync(cancellationToken); },
                "Voltou para a música anterior.",
                "voltar para a música anterior");

        public static Task<HubResult> SetVolumeAsync(int volumePercent, CancellationToken cancellationToken = default)
            => ExecuteAsync(
                async () => { await _manager!.SetVolumeAsync(volumePercent, cancellationToken); },
                $"Volume ajustado para {volumePercent}%.",
                "ajustar o volume");

        public static HubResult GetProgressBar()
        {
            try
            {
                EnsureReady();
                return HubResult.Ok(_manager!.GetCurrentTrackProgressBar(), "Barra de progresso obtida.");
            }
            catch (Exception ex)
            {
                return HubResult.Fail(BuildFriendlyError(ex, "obter o progresso da música"));
            }
        }

        public static Task<HubResult> GetCurrentTrackAsync(CancellationToken cancellationToken = default)
            => ExecuteAsync(
                () => _manager!.GetCurrentTrackAsync(cancellationToken),
                "Música atual obtida.",
                "obter a música atual");

        #endregion

        #region Fila

        public static Task<HubResult> GetQueueAsync(int limit = 5, CancellationToken cancellationToken = default)
            => ExecuteAsync(
                () => _manager!.GetQueueAsync(limit, cancellationToken),
                "Fila obtida com sucesso.",
                "obter a fila de músicas");

        public static Task<HubResult> AddToQueueAsync(string input, string userId, string userName, CancellationToken cancellationToken = default)
            => ExecuteAsync(
                async () =>
                {
                    var track = await _manager!.AddToQueueAsync(input, userId, userName, cancellationToken);
                    return (track, $"'{track.Media.Title}' foi adicionada à fila!");
                },
                "adicionar música à fila");

        public static async Task<HubResult> RemoveLastAddedFromQueueAsync(string userId, bool isModOrStreamer = false, CancellationToken cancellationToken = default)
        {
            try
            {
                EnsureReady();
                var (success, removedItem, message) = await _manager!.RemoveLastAddedFromQueueAsync(userId, isModOrStreamer, cancellationToken);
                return success
                    ? HubResult.Ok(removedItem!, message)
                    : HubResult.Fail(message);
            }
            catch (Exception ex)
            {
                return HubResult.Fail(BuildFriendlyError(ex, "remover a última música da fila"));
            }
        }

        public static HubResult GetPendingUserQueue()
        {
            try
            {
                EnsureReady();
                return HubResult.Ok(_manager!.GetPendingUserQueue(), "Fila pendente obtida.");
            }
            catch (Exception ex)
            {
                return HubResult.Fail(BuildFriendlyError(ex, "obter a fila pendente"));
            }
        }

        #endregion

        #region Playlist / Skip

        public static async Task<HubResult> AddCurrentTrackToPlaylistAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                EnsureReady();
                var (success, message) = await _manager!.AddCurrentTrackToPlaylistAsync(cancellationToken);
                return new HubResult(success, message);
            }
            catch (Exception ex)
            {
                return HubResult.Fail(BuildFriendlyError(ex, "adicionar a música à playlist"));
            }
        }

        public static async Task<HubResult> ForceSkipAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                EnsureReady();
                var (success, message) = await _manager!.ForceSkipAsync(cancellationToken);
                return new HubResult(success, message);
            }
            catch (Exception ex)
            {
                return HubResult.Fail(BuildFriendlyError(ex, "pular a música"));
            }
        }

        public static async Task<HubResult> VoteSkipAsync(string userId, CancellationToken cancellationToken = default)
        {
            try
            {
                EnsureReady();
                var voteResult = await _manager!.VoteSkipAsync(userId, cancellationToken);
                return voteResult.Accepted
                    ? HubResult.Ok(voteResult, voteResult.Message)
                    : HubResult.Fail(voteResult.Message);
            }
            catch (Exception ex)
            {
                return HubResult.Fail(BuildFriendlyError(ex, "registrar o voto de skip"));
            }
        }

        #endregion

        /// <summary>Para o polling e libera a instância atual. Útil antes de trocar de conta/configuração do zero.</summary>
        public static void Shutdown()
        {
            _pollingCts?.Cancel();
            _pollingCts?.Dispose();
            _pollingCts = null;

            _manager?.Dispose();
            _manager = null;

            _isInitialized = false;
        }

        #region Helpers Internos

        private static void EnsureReady()
        {
            if (!_isInitialized || _manager == null)
                throw new InvalidOperationException(
                    "O SpotifyHub ainda não foi inicializado. Chame 'SpotifyHub.InitializeAsync()' antes de executar qualquer ação (normalmente numa subação de inicialização, disparada no início da live).");
        }

        private static string BuildFriendlyError(Exception ex, string acao) => ex switch
        {
            InvalidOperationException => ex.Message,
            OperationCanceledException => $"A operação de {acao} foi cancelada.",
            _ => $"Erro ao {acao}: {ex.Message}"
        };

        private static async Task<HubResult> ExecuteAsync(Func<Task> action, string successMessage, string acao)
        {
            try
            {
                EnsureReady();
                await action();
                return HubResult.Ok(successMessage);
            }
            catch (Exception ex)
            {
                return HubResult.Fail(BuildFriendlyError(ex, acao));
            }
        }

        private static async Task<HubResult> ExecuteAsync<T>(Func<Task<T>> action, string successMessage, string acao)
        {
            try
            {
                EnsureReady();
                var data = await action();
                return HubResult.Ok(data!, successMessage);
            }
            catch (Exception ex)
            {
                return HubResult.Fail(BuildFriendlyError(ex, acao));
            }
        }

        private static async Task<HubResult> ExecuteAsync<T>(Func<Task<(T Data, string Message)>> action, string acao)
        {
            try
            {
                EnsureReady();
                var (data, message) = await action();
                return HubResult.Ok(data!, message);
            }
            catch (Exception ex)
            {
                return HubResult.Fail(BuildFriendlyError(ex, acao));
            }
        }

        #endregion
    }
}