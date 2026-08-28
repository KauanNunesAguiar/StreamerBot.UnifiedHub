using System.Net;
using System.Text;
using StreamerBot.UnifiedHub.Core.Services;
using StreamerBot.UnifiedHub.Integrations.Overlay.Models;

namespace StreamerBot.UnifiedHub.Integrations.Overlay.Services
{
    /// <summary>
    /// Gera o HTML da tela de configurações do overlay de chat manualmente, sem RazorLight
    /// (mesmo motivo documentado em SpotifyHtmlTemplates).
    /// </summary>
    public static class OverlayHtmlTemplates
    {
        private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        private static readonly string SharedCss = EmbeddedResourceReader.ReadText("Core.Assets.SharedStyles.css");
        private static readonly string ChatCss = EmbeddedResourceReader.ReadText("Core.Assets.ChatSettingsStyles.css");

        public static string RenderSettings(OverlaySettingsViewModel model)
        {
            var config = model.Config;
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang=\"pt-BR\"><head><meta charset=\"UTF-8\">");
            sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.Append("<title>Configurações do Overlay de Chat - StreamerBot Unified Hub</title>");
            sb.Append("<style>").Append(SharedCss).Append(ChatCss);
            sb.Append(@"
                .container { max-width: 520px; }
                .section-title { font-size: 13px; font-weight: 700; letter-spacing: 0.5px; text-transform: uppercase; color: var(--primary); margin: 20px 0 12px 0; border-bottom: 1px solid var(--border); padding-bottom: 6px; }
                .mode-options { display: flex; gap: 10px; }
                .mode-options label { flex: 1; display: flex; align-items: center; gap: 8px; background-color: var(--item-bg); border: 1px solid transparent; border-radius: 8px; padding: 10px 12px; cursor: pointer; font-size: 13px; }
                .mode-options label:has(input:checked) { border-color: var(--primary); background-color: var(--item-bg-hover); }
                .success { background-color: rgba(29, 185, 84, 0.15); border: 1px solid var(--primary); color: var(--primary-hover); padding: 12px 16px; border-radius: 8px; font-size: 13px; margin-bottom: 20px; }
                .field-row { display: flex; gap: 12px; }
                .field-row .form-group { flex: 1; }
                .preview-wrapper { position: relative; flex: 1; min-height: 260px; border-radius: 8px; overflow: hidden; margin-bottom: 12px; background-image: linear-gradient(45deg, #2a2a2a 25%, transparent 25%), linear-gradient(-45deg, #2a2a2a 25%, transparent 25%), linear-gradient(45deg, transparent 75%, #2a2a2a 75%), linear-gradient(-45deg, transparent 75%, #2a2a2a 75%); background-size: 20px 20px; background-position: 0 0, 0 10px, 10px -10px, -10px 0px; background-color: #1a1a1a; }
                .preview-frame { width: 100%; height: 100%; border: none; }
                .btn-secondary { width: 100%; background-color: var(--item-bg); color: var(--text-main); border: 1px solid var(--border); padding: 12px; border-radius: 50px; font-size: 13px; font-weight: 700; cursor: pointer; margin-top: 8px; }
                .btn-secondary:hover { background-color: var(--item-bg-hover); }
            ");
            sb.Append("</style></head><body><div class=\"container wide\">");
            sb.Append("<div class=\"header\"><div class=\"logo-icon\">💬</div><h1>Configurações do Overlay de Chat</h1><p>Ajuste conexão, comportamento e visual do overlay</p></div>");

            if (!string.IsNullOrEmpty(model.Error))
                sb.Append("<div class=\"error\">").Append(E(model.Error)).Append("</div>");
            else if (model.Saved)
                sb.Append("<div class=\"success\">Configurações salvas com sucesso!</div>");

            sb.Append("<div class=\"settings-grid\">");
            sb.Append("<div class=\"col\">");
            sb.Append("<form method=\"POST\">");

            sb.Append("<div class=\"section-title\">Conexão</div>");
            sb.Append("<div class=\"field-row\">");
            sb.Append("<div class=\"form-group\"><label for=\"port\">Porta</label>");
            sb.Append("<input type=\"number\" id=\"port\" name=\"port\" min=\"1024\" max=\"65535\" value=\"").Append(config.Port).Append("\" required></div>");
            sb.Append("<div class=\"form-group\"><label for=\"endpoint\">Endpoint WebSocket</label>");
            sb.Append("<input type=\"text\" id=\"endpoint\" name=\"endpoint\" value=\"").Append(E(config.Endpoint)).Append("\" required></div>");
            sb.Append("</div>");
            sb.Append("<span class=\"field-hint\">Alterar a porta ou o endpoint reinicia o servidor do overlay - atualize a URL no Browser Source do OBS depois de salvar.</span>");

            sb.Append("<div class=\"section-title\">Comportamento</div>");
            sb.Append("<div class=\"form-group\"><label>Modo de Exibição</label>");
            sb.Append("<div class=\"mode-options\">");
            sb.Append("<label><input type=\"radio\" name=\"mode\" value=\"fadeout\"").Append(config.Mode == ChatOverlayMode.FadeOut ? " checked" : "").Append("> Transparente (fade out)</label>");
            sb.Append("<label><input type=\"radio\" name=\"mode\" value=\"permanent\"").Append(config.Mode == ChatOverlayMode.Permanent ? " checked" : "").Append("> Permanente</label>");
            sb.Append("</div></div>");

            sb.Append("<div class=\"field-row\">");
            sb.Append("<div class=\"form-group\"><label for=\"maxMessages\">Máx. de Mensagens na Tela</label>");
            sb.Append("<input type=\"number\" id=\"maxMessages\" name=\"maxMessages\" min=\"1\" max=\"200\" value=\"").Append(config.MaxMessages).Append("\" required></div>");
            sb.Append("<div class=\"form-group\"><label for=\"fadeTimeMs\">Tempo de Fade (ms)</label>");
            sb.Append("<input type=\"number\" id=\"fadeTimeMs\" name=\"fadeTimeMs\" min=\"1000\" step=\"500\" value=\"").Append(config.FadeTimeMs).Append("\" required></div>");
            sb.Append("</div>");

            sb.Append("<div class=\"section-title\">Visual</div>");
            sb.Append("<div class=\"field-row\">");
            sb.Append("<div class=\"form-group\"><label for=\"emoteSize\">Tamanho dos Emotes (px)</label>");
            sb.Append("<input type=\"number\" id=\"emoteSize\" name=\"emoteSize\" min=\"8\" max=\"128\" value=\"").Append(config.EmoteSize).Append("\" required></div>");
            sb.Append("<div class=\"form-group\"><label for=\"badgeSize\">Tamanho dos Badges (px)</label>");
            sb.Append("<input type=\"number\" id=\"badgeSize\" name=\"badgeSize\" min=\"8\" max=\"64\" value=\"").Append(config.BadgeSize).Append("\" required></div>");
            sb.Append("</div>");

            sb.Append("<div class=\"message-header\" style=\"margin-top: 16px;\">");
            sb.Append("<label style=\"margin-bottom: 0;\">Exibir Badges (MOD/VIP/SUB/BR)</label>");
            sb.Append("<label class=\"toggle-switch\">");
            sb.Append("<input type=\"checkbox\" name=\"showBadges\"").Append(config.ShowBadges ? " checked" : "").Append(">");
            sb.Append("<span class=\"toggle-track\"></span><span class=\"toggle-label\">").Append(config.ShowBadges ? "Habilitado" : "Desabilitado").Append("</span></label></div>");

            sb.Append("<div class=\"section-title\">CSS Customizado</div>");
            sb.Append("<div class=\"form-group\"><label for=\"customCss\">CSS Adicional (avançado)</label>");
            sb.Append("<textarea id=\"customCss\" name=\"customCss\" rows=\"6\" placeholder=\".msg { ... }\">").Append(E(config.CustomCss)).Append("</textarea>");
            sb.Append("<span class=\"field-hint\">Aplicado direto no overlay em tempo real. CSS inválido pode quebrar o layout.</span></div>");
            sb.Append("</div>"); // fecha .col esquerda

            sb.Append("<div class=\"col col-sticky\">");
            sb.Append("<div class=\"section-title\">Preview ao Vivo</div>");
            sb.Append("<div class=\"preview-wrapper\"><iframe id=\"previewFrame\" class=\"preview-frame\" src=\"/\"></iframe></div>");
            sb.Append("<button type=\"button\" id=\"sendTestMsgBtn\" class=\"btn-secondary\">Enviar Mensagem de Teste</button>");
            sb.Append("</div>"); // fecha .col direita

            sb.Append("</div>"); // fecha .settings-grid

            sb.Append("<button type=\"submit\" class=\"btn-submit\">Salvar Configurações</button>");
            sb.Append("</form>");

            sb.Append(@"<script>
                (function() {
                    var frame = document.getElementById('previewFrame');
                    var testBtn = document.getElementById('sendTestMsgBtn');
                    var fieldIds = ['maxMessages', 'fadeTimeMs', 'emoteSize', 'badgeSize', 'customCss'];

                    function currentState() {
                        var modeInput = document.querySelector('input[name=""mode""]:checked');
                        return {
                            type: 'previewConfig',
                            state: {
                                mode: modeInput ? modeInput.value : 'fadeout',
                                maxMessages: parseInt(document.getElementById('maxMessages').value, 10) || 50,
                                fadeTimeMs: parseInt(document.getElementById('fadeTimeMs').value, 10) || 12000,
                                emoteSize: parseInt(document.getElementById('emoteSize').value, 10) || 28,
                                badgeSize: parseInt(document.getElementById('badgeSize').value, 10) || 18,
                                showBadges: document.querySelector('input[name=""showBadges""]').checked
                                customCss: document.getElementById('customCss').value
                            }
                        };
                    }

                    function sendPreviewConfig() {
                        if (frame.contentWindow) frame.contentWindow.postMessage(currentState(), '*');
                    }

                    frame.addEventListener('load', sendPreviewConfig);
                    document.querySelectorAll('input[name=""mode""], input[name=""showBadges""]').forEach(function(el) {
                        el.addEventListener('change', sendPreviewConfig);
                    });
                    fieldIds.forEach(function(id) {
                        document.getElementById(id).addEventListener('input', sendPreviewConfig);
                    });

                    var testMessages = [
                        { platform: 'twitch', user: 'ChatterTwitch', color: '#9146FF', isBroadcaster: false, isModerator: true, isVip: false, isSubscriber: true, message: 'Mensagem de teste com um emote Kappa', emotes: '25:31-36' },
                        { platform: 'youtube', user: 'ChatterYoutube', color: '#FF0000', isBroadcaster: false, isModerator: false, isVip: false, isSubscriber: false, message: 'Mensagem de teste do YouTube' }
                    ];
                    var testIndex = 0;

                    testBtn.addEventListener('click', function() {
                        if (!frame.contentWindow) return;
                        var msg = testMessages[testIndex % testMessages.length];
                        testIndex++;
                        frame.contentWindow.postMessage({ type: 'previewChat', message: msg }, '*');
                    });
                })();
                </script>");
            sb.Append("</div></body></html>");
            return sb.ToString();
        }
    }
}