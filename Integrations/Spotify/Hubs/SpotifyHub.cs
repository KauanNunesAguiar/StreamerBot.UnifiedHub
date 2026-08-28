using System;
using System.Collections.Generic;
using System.Text;
using StreamerBot.UnifiedHub.Core.Models;
using StreamerBot.UnifiedHub.Core.Services.Chat;
using StreamerBot.UnifiedHub.Core.Services.Config;
using StreamerBot.UnifiedHub.Core.Services.Execution;
using StreamerBot.UnifiedHub.Core.Services.Http;
using StreamerBot.UnifiedHub.Core.Services.OAuth;
using StreamerBot.UnifiedHub.Integrations.Spotify.Extensions;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;
using StreamerBot.UnifiedHub.Integrations.Spotify.Services;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Hubs
{
    public static class SpotifyHub
    {
        private static SpotifyManager? _manager;
        private static CancellationTokenSource? _pollingCts;
        private static readonly SemaphoreSlim _initLock = new(1, 1);
        private static volatile bool _isInitialized;

        public static bool IsInitialized => _isInitialized;

        private static readonly HubExecutionHelper _executor = new(
            () => _isInitialized && _manager != null,
            "O SpotifyHub ainda não foi inicializado. Chame 'SpotifyHub.InitializeAsync()' antes de executar qualquer ação (normalmente numa subação de inicialização, disparada no início da live).");
        private static Func<IEnumerable<HubCommandInfo>>? _commandProvider;

        public static event EventHandler<SpotifyTrackInfo>? OnTrackChanged;
        public static event EventHandler<SpotifyTrackInfo>? OnPlayerUpdated;
        public static event EventHandler<ChatMessageEventArgs>? OnChatMessage;

        #region Inicialização

        /// <summary>
        /// Inicializa toda a integração: carrega configurações salvas, autentica com o Spotify
        /// (abrindo o navegador automaticamente se necessário) e inicia o monitoramento contínuo
        /// da música tocando. Idempotente - seguro chamar múltiplas vezes.
        /// </summary>
        public static async Task<HubResult> InitializeAsync(int? pollingIntervalMs = null, CancellationToken cancellationToken = default)
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

                var httpServer = new EmbedIoHttpServer();
                var browserService = new SystemBrowser();
                var oauthHandler = new SpotifyOAuthHandler(httpServer, browserService, configManager);

                var youTubeLookup = new YouTubeOEmbedLookup(HubServiceProvider.GetHttpClient("YouTube"));
                var playerService = new SpotifyPlayerService(youTubeLookup);

                var manager = new SpotifyManager(oauthHandler, playerService, spotifyConfig, configManager);

                manager.OnTrackChanged += (sender, track) => OnTrackChanged?.Invoke(sender, track);
                manager.OnPlayerUpdated += (sender, track) => OnPlayerUpdated?.Invoke(sender, track);
                manager.OnChatMessage += (sender, args) => OnChatMessage?.Invoke(sender, args);

                await manager.InitializeAsync(cancellationToken);

                _manager = manager;

                _pollingCts = new CancellationTokenSource();
                _ = manager.StartPollingAsync(pollingIntervalMs ?? spotifyConfig.PollingIntervalMs, _pollingCts.Token);

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
            => _executor.ExecuteAsync(
                async () => { await _manager!.ReconfigureAsync(cancellationToken); },
                "Reconfiguração concluída com sucesso.",
                "reconfigurar o Spotify");

        /// <summary>Abre o navegador direto na tela de configurações (playlist/mensagens), sem repetir o login.</summary>
        public static Task<HubResult> OpenSettingsUiAsync(CancellationToken cancellationToken = default)
            => _executor.ExecuteAsync(
                () => _manager!.OpenSettingsUiAsync(cancellationToken),
                "abrir as configurações");

        public static void SetCommandProvider(Func<IEnumerable<HubCommandInfo>> provider)
            => _commandProvider = provider;

        #endregion

        #region Configurações Rápidas

        public static HubResult GetSettings()
            => _executor.Execute(
                () => _manager!.GetSettingsSnapshot(),
                "Configurações obtidas.",
                "obter as configurações");

        public static HubResult SetVoteSkipThreshold(int threshold)
            => _executor.Execute(
                () => _manager!.UpdateSettings(c => c.VoteSkipThreshold = threshold),
                $"Limite de voto para pular ajustado para {threshold}.",
                "ajustar o limite de voteskip");

        public static HubResult SetQueueSize(int size)
            => _executor.Execute(
                () => _manager!.UpdateSettings(c => c.QueueSize = size),
                $"Tamanho da fila exibida ajustado para {size}.",
                "ajustar o tamanho da fila");

        public static HubResult SetPollingIntervalMs(int intervalMs)
            => _executor.Execute(
                () => _manager!.UpdateSettings(c => c.PollingIntervalMs = intervalMs),
                $"Intervalo de monitoramento ajustado para {intervalMs}ms.",
                "ajustar o intervalo de monitoramento");

        public static HubResult SetBotLabel(string BotLabel)
            => _executor.Execute(
                () => _manager!.UpdateSettings(c => c.BotLabel = BotLabel),
                $"Rótulo  do bot ajustado para '{BotLabel}'.",
                "ajustar o rotulo do bot");

        public static HubResult SetMessageEnabled(string key, bool enabled)
            => _executor.Execute(
                () => _manager!.UpdateSettings(c => c.MessageEnabled[key] = enabled),
                $"Mensagem '{key}' {(enabled ? "habilitada" : "desabilitada")}.",
                "alterar o estado da mensagem");

        public static HubResult SetMessageTemplate(string key, string template)
            => _executor.Execute(
                () => _manager!.UpdateSettings(c => c.Messages[key] = template),
                $"Mensagem '{key}' atualizada.",
                "atualizar o texto da mensagem");

        #endregion

        #region Player

        public static Task<HubResult> PauseAsync(string user = "", CancellationToken cancellationToken = default)
            => _executor.ExecuteAsync(
                async () => { await _manager!.PauseAsync(user, cancellationToken); },
                "Reprodução pausada.",
                "pausar a música");

        public static Task<HubResult> ResumeAsync(string user = "", CancellationToken cancellationToken = default)
            => _executor.ExecuteAsync(
                async () => { await _manager!.ResumeAsync(user, cancellationToken); },
                "Reprodução retomada.",
                "retomar a música");

        public static Task<HubResult> PreviousAsync(string user = "", CancellationToken cancellationToken = default)
            => _executor.ExecuteAsync(
                async () => { await _manager!.SkipToPreviousAsync(user, cancellationToken); },
                "Voltou para a música anterior.",
                "voltar para a música anterior");

        public static Task<HubResult> SetVolumeAsync(int volumePercent, string user = "", CancellationToken cancellationToken = default)
            => _executor.ExecuteAsync(
                async () => { await _manager!.SetVolumeAsync(volumePercent, user, cancellationToken); },
                $"Volume ajustado para {volumePercent}%.",
                "ajustar o volume");

        public static HubResult GetProgressBar()
            => _executor.Execute(
                () => _manager!.GetCurrentTrackProgressBar(),
                "Barra de progresso obtida.",
                "obter o progresso da música");

        public static Task<HubResult> GetCurrentTrackAsync(CancellationToken cancellationToken = default)
            => _executor.ExecuteAsync(
                () => _manager!.GetCurrentTrackAsync(cancellationToken),
                "Música atual obtida.",
                "obter a música atual");

        #endregion

        #region Fila

        public static Task<HubResult> GetQueueAsync(int? limit = null, CancellationToken cancellationToken = default)
            => _executor.ExecuteAsync(
                () => _manager!.GetQueueAsync(limit, cancellationToken),
                "Fila obtida com sucesso.",
                "obter a fila de músicas");

        public static Task<HubResult> AddToQueueAsync(string input, string userId, string userName, CancellationToken cancellationToken = default)
            => _executor.ExecuteAsync(
                async () =>
                {
                    var track = await _manager!.AddToQueueAsync(input, userId, userName, cancellationToken);
                    return (track, $"'{track.Media.Title}' foi adicionada à fila!");
                },
                "adicionar música à fila");

        public static Task<HubResult> RemoveLastAddedFromQueueAsync(string userId, CancellationToken cancellationToken = default)
            => _executor.ExecuteAsync(
                async () =>
                {
                    var (success, removedItem, message) = await _manager!.RemoveLastAddedFromQueueAsync(userId, cancellationToken);
                    return success ? HubResult.Ok(removedItem!, message) : HubResult.Fail(message);
                },
                "remover a última música da fila");

        public static HubResult GetPendingUserQueue()
            => _executor.Execute(
                () => _manager!.GetPendingUserQueue(),
                "Fila pendente obtida.",
                "obter a fila pendente");

        #endregion

        #region Playlist / Skip

        public static Task<HubResult> ShowPlaylistInfoAsync(CancellationToken cancellationToken = default)
            => _executor.ExecuteAsync(
                () => _manager!.ShowPlaylistInfoAsync(cancellationToken),
                "Informações da playlist exibidas.",
                "exibir as informações da playlist");

        public static Task<HubResult> AddCurrentTrackToPlaylistAsync(string user = "", CancellationToken cancellationToken = default)
            => _executor.ExecuteAsync(
                async () =>
                {
                    var (success, message) = await _manager!.AddCurrentTrackToPlaylistAsync(user, cancellationToken);
                    return new HubResult(success, message);
                },
                "adicionar a música à playlist");

        public static Task<HubResult> ForceSkipAsync(string user = "", CancellationToken cancellationToken = default)
            => _executor.ExecuteAsync(
                async () =>
                {
                    var (success, message) = await _manager!.ForceSkipAsync(user, cancellationToken);
                    return new HubResult(success, message);
                },
                "pular a música");


        public static Task<HubResult> VoteSkipAsync(string user, string userId, CancellationToken cancellationToken = default)
            => _executor.ExecuteAsync(
                async () =>
                {
                    var voteResult = await _manager!.VoteSkipAsync(user, userId, cancellationToken);
                    return voteResult.Accepted
                        ? HubResult.Ok(voteResult, voteResult.Message)
                        : HubResult.Fail(voteResult.Message);
                },
                "registrar o voto de skip");

        public static Task<HubResult> NotifyNoPermissionAsync(string user = "", CancellationToken cancellationToken = default)
            => _executor.ExecuteAsync(
                async () => { _manager!.NotifyNoPermission(user); await Task.CompletedTask; },
                "Mensagem de sem permissão enviada.",
                "notificar falta de permissão");

        public static Task<HubResult> NotifyCooldownAsync(string user = "", CancellationToken cancellationToken = default)
        => _executor.ExecuteAsync(
            async () => { _manager!.NotifyCooldown(user); await Task.CompletedTask; },
            "Mensagem de cooldown enviada.",
            "notificar cooldown");

        public static Task<HubResult> ShowSongHelpAsync(string user = "", CancellationToken cancellationToken = default)
            => _executor.ExecuteAsync(
                async () =>
                {
                    string listaComandos = BuildCommandsListText();
                    _manager!.ShowHelp(user, listaComandos);
                    await Task.CompletedTask;
                },
                "Ajuda exibida.",
                "exibir a ajuda");

        private static string BuildCommandsListText()
        {
            if (_commandProvider == null)
                return "Lista de comandos indisponível (SetCommandProvider não foi configurado).";

            return CommandHelpTextBuilder.Build(SpotifyMessageCatalog.Definitions, _commandProvider());
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

        private static string BuildFriendlyError(Exception ex, string acao) => ex switch
        {
            InvalidOperationException => ex.Message,
            OperationCanceledException => $"A operação de {acao} foi cancelada.",
            _ => $"Erro ao {acao}: {ex.Message}"
        };

        #endregion
    }
}