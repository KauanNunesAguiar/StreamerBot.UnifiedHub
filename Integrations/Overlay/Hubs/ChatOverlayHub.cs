using System;
using StreamerBot.UnifiedHub.Core.Abstractions;
using StreamerBot.UnifiedHub.Core.Models;
using StreamerBot.UnifiedHub.Core.Services;
using StreamerBot.UnifiedHub.Integrations.Overlay.Extensions;
using StreamerBot.UnifiedHub.Integrations.Overlay.Models;
using StreamerBot.UnifiedHub.Integrations.Overlay.Services;

namespace StreamerBot.UnifiedHub.Integrations.Overlay.Hubs
{
    public static class ChatOverlayHub
    {
        private static readonly OverlayWebSocketServer _server = new();
        private static volatile bool _isInitialized;
        private static IConfigManager? _configManager;
        private static ChatOverlayConfig _config = new();
        private static int _runningPort;
        private static string _runningEndpoint = "/ws";

        private static readonly HubExecutionHelper _executor = new(
            () => _isInitialized,
            "O overlay de chat ainda não foi iniciado. Chame 'ChatOverlayHub.Start()' primeiro.");

        public static HubResult Start(int? port = null)
        {
            if (_isInitialized)
                return HubResult.Ok("Overlay de chat já estava rodando.");

            try
            {
                _configManager = new ConfigManager();
                var appConfig = _configManager.Load();
                _config = appConfig.GetChatOverlayConfig();

                if (port.HasValue && port.Value > 0)
                    _config.Port = port.Value;

                StartServer();
                _isInitialized = true;
                return HubResult.Ok($"Overlay de chat disponível em http://127.0.0.1:{_config.Port}/");
            }
            catch (Exception ex)
            {
                return HubResult.Fail($"Erro ao iniciar o overlay de chat: {ex.Message}");
            }
        }

        public static void Stop()
        {
            _server.Stop();
            _isInitialized = false;
        }

        public static HubResult PushTwitchMessage(
            string user, string message, string? color = null, string? emotes = null,
            bool isBroadcaster = false, bool isModerator = false, bool isVip = false, bool isSubscriber = false)
                => Push("twitch", user, message, color, emotes, isBroadcaster, isModerator, isVip, isSubscriber);

        public static HubResult PushYoutubeMessage(string user, string message, string? color = null)
            => Push("youtube", user, message, color, null, false, false, false, false);

        private static HubResult Push(string platform, string user, string message, string? color, string? emotes,
            bool isBroadcaster, bool isModerator, bool isVip, bool isSubscriber)
            => _executor.Execute(
                () => _server.PushChatMessage(new ChatOverlayMessage(platform, user, message, color, emotes, isBroadcaster, isModerator, isVip, isSubscriber, DateTime.UtcNow)),
                "Mensagem enviada ao overlay.",
                "enviar mensagem ao overlay");

        public static HubResult SetPort(int port)
            => _executor.Execute(() =>
            {
                _config.Port = port;
                SaveConfig();
                RestartServer();
            }, $"Porta do overlay ajustada para {port}. Atualize a URL no Browser Source.", "ajustar a porta do overlay");

        public static HubResult SetEndpoint(string endpoint)
            => _executor.Execute(() =>
            {
                _config.Endpoint = string.IsNullOrWhiteSpace(endpoint) ? "/ws" : endpoint;
                SaveConfig();
                RestartServer();
            }, $"Endpoint do overlay ajustado para '{_config.Endpoint}'.", "ajustar o endpoint do overlay");

        public static HubResult SetMaxMessages(int max)
            => _executor.Execute(() => { _config.MaxMessages = max; SaveConfig(); _server.PushConfigUpdate(); },
                $"Máximo de mensagens ajustado para {max}.", "ajustar o máximo de mensagens do overlay");

        public static HubResult SetFadeTimeMs(int fadeTimeMs)
            => _executor.Execute(() => { _config.FadeTimeMs = fadeTimeMs; SaveConfig(); _server.PushConfigUpdate(); },
                $"Tempo de fade ajustado para {fadeTimeMs}ms.", "ajustar o tempo de fade do overlay");

        public static HubResult SetEmoteSize(int emoteSize)
            => _executor.Execute(() => { _config.EmoteSize = emoteSize; SaveConfig(); _server.PushConfigUpdate(); },
                $"Tamanho dos emotes ajustado para {emoteSize}px.", "ajustar o tamanho dos emotes do overlay");

        public static HubResult SetBadgeSize(int badgeSize)
            => _executor.Execute(() => { _config.BadgeSize = badgeSize; SaveConfig(); _server.PushConfigUpdate(); },
                $"Tamanho dos badges ajustado para {badgeSize}px.", "ajustar o tamanho dos badges do overlay");

        public static HubResult SetShowBadges(bool show)
            => _executor.Execute(() => { _config.ShowBadges = show; SaveConfig(); _server.PushConfigUpdate(); },
                $"Exibição de badges {(show ? "habilitada" : "desabilitada")}.", "ajustar a exibição de badges do overlay");

        public static HubResult SetModeFadeOut()
            => _executor.Execute(() => { _config.Mode = ChatOverlayMode.FadeOut; SaveConfig(); _server.PushConfigUpdate(); },
                "Modo do overlay: fade out.", "alterar o modo do overlay");

        public static HubResult SetModePermanent()
            => _executor.Execute(() => { _config.Mode = ChatOverlayMode.Permanent; SaveConfig(); _server.PushConfigUpdate(); },
                "Modo do overlay: permanente.", "alterar o modo do overlay");

        private static void StartServer()
        {
            _server.Start(_config, OnSettingsSavedAsync);
            _runningPort = _config.Port;
            _runningEndpoint = _config.Endpoint;
        }

        private static void RestartServer()
        {
            _server.Stop();
            StartServer();
        }

        private static async Task OnSettingsSavedAsync(ChatOverlayConfig updated)
        {
            _config = updated;
            SaveConfig();

            if (updated.Port != _runningPort || updated.Endpoint != _runningEndpoint)
            {
                await Task.Delay(500); // deixa a resposta HTTP do form ser entregue antes de derrubar o servidor
                RestartServer();
            }
        }

        public static Task<HubResult> OpenSettingsUiAsync()
            => _executor.ExecuteAsync(async () =>
            {
                new SystemBrowser().OpenUrl($"http://127.0.0.1:{_config.Port}/settings");
                await Task.CompletedTask;
            }, "Configurações do overlay abertas no navegador.", "abrir as configurações do overlay");

        private static void SaveConfig()
        {
            if (_configManager == null) return;
            var appConfig = _configManager.Load();
            appConfig.SetChatOverlayConfig(_config);
            _configManager.Save(appConfig);
        }
    }
}