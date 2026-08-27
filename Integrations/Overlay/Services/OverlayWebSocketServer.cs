using System.Text;
using System.Web;
using EmbedIO;
using EmbedIO.Actions;
using StreamerBot.UnifiedHub.Core.Services;
using StreamerBot.UnifiedHub.Integrations.Overlay.Models;

namespace StreamerBot.UnifiedHub.Integrations.Overlay.Services
{
    public class OverlayWebSocketServer
    {
        private static readonly string OverlayHtmlTemplate = EmbeddedResourceReader.ReadText("Integrations.Overlay.Assets.chat-overlay.html");

        private WebServer? _server;
        private ChatSocketModule? _socketModule;
        private ChatOverlayConfig _config = new();
        private Func<ChatOverlayConfig, Task>? _onSettingsSaved;

        public void Start(ChatOverlayConfig config, Func<ChatOverlayConfig, Task>? onSettingsSaved = null)
        {
            _config = config;
            _onSettingsSaved = onSettingsSaved;
            string html = OverlayHtmlTemplate.Replace("{{WS_ENDPOINT}}", _config.Endpoint);

            _socketModule = new ChatSocketModule(_config.Endpoint, BuildStatePayload);

            _server = new WebServer(o => o.WithUrlPrefix($"http://127.0.0.1:{_config.Port}/"))
                .WithModule(_socketModule)
                .WithModule(new ActionModule("/settings", HttpVerbs.Any, HandleSettingsAsync))
                .WithModule(new ActionModule("/", HttpVerbs.Get, ctx => ctx.SendStringAsync(html, "text/html", Encoding.UTF8)));

            _ = _server.RunAsync();
        }

        private async Task HandleSettingsAsync(IHttpContext ctx)
        {
            string? error = null;
            bool saved = false;

            if (ctx.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                string body = await ctx.GetRequestBodyAsStringAsync();
                var form = HttpUtility.ParseQueryString(body);

                int maxMessages = 0, fadeTimeMs = 0, emoteSize = 0, badgeSize = 0;

                bool valid = int.TryParse(form["port"], out int port) && port > 0
                    && int.TryParse(form["maxMessages"], out maxMessages) && maxMessages > 0
                    && int.TryParse(form["fadeTimeMs"], out fadeTimeMs) && fadeTimeMs > 0
                    && int.TryParse(form["emoteSize"], out emoteSize) && emoteSize > 0
                    && int.TryParse(form["badgeSize"], out badgeSize) && badgeSize > 0;

                if (valid)
                {
                    _config.Port = port;
                    _config.Endpoint = string.IsNullOrWhiteSpace(form["endpoint"]) ? "/ws" : form["endpoint"]!;
                    _config.MaxMessages = maxMessages;
                    _config.FadeTimeMs = fadeTimeMs;
                    _config.EmoteSize = emoteSize;
                    _config.BadgeSize = badgeSize;
                    _config.ShowBadges = !string.IsNullOrEmpty(form["showBadges"]);
                    _config.Mode = form["mode"] == "permanent" ? ChatOverlayMode.Permanent : ChatOverlayMode.FadeOut;

                    if (_onSettingsSaved != null)
                        await _onSettingsSaved(_config);

                    PushConfigUpdate();
                    saved = true;
                }
                else
                {
                    error = "Preencha todos os campos numéricos corretamente.";
                }
            }

            string html = OverlayHtmlTemplates.RenderSettings(new OverlaySettingsViewModel { Config = _config, Error = error, Saved = saved });
            await ctx.SendStringAsync(html, "text/html; charset=utf-8", Encoding.UTF8);
        }

        public void Stop()
        {
            _server?.Dispose();
            _server = null;
        }

        public void PushConfigUpdate()
            => _ = _socketModule?.BroadcastJsonAsync(BuildStatePayload());

        public void PushChatMessage(ChatOverlayMessage message)
            => _ = _socketModule?.BroadcastJsonAsync(new
            {
                type = "chat",
                platform = message.Platform,
                user = message.UserName,
                message = message.Message,
                color = message.Color,
                emotes = message.Emotes,
                isBroadcaster = message.IsBroadcaster,
                isModerator = message.IsModerator,
                isVip = message.IsVip,
                isSubscriber = message.IsSubscriber
            });

        private object BuildStatePayload() => new
        {
            type = "state",
            mode = _config.Mode.ToString().ToLowerInvariant(),
            maxMessages = _config.MaxMessages,
            fadeTimeMs = _config.FadeTimeMs,
            emoteSize = _config.EmoteSize,
            badgeSize = _config.BadgeSize,
            showBadges = _config.ShowBadges
        };
    }
}