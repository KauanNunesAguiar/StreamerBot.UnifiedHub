using System.Net;
using System.Text;
using StreamerBot.UnifiedHub.Core.Services.Html;
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

        public static string RenderSettings(OverlaySettingsViewModel model)
        {
            var config = model.Config;

            // ---------- Coluna esquerda ----------
            var left = new StringBuilder();
            left.Append("<form method=\"POST\">");

            left.Append(HtmlComponents.SectionTitle("Conexão"));
            left.Append("<div class=\"field-row\">");
            left.Append(HtmlComponents.FormGroup(
                "port", "Porta",
                $"<input type=\"number\" id=\"port\" name=\"port\" min=\"1024\" max=\"65535\" value=\"{config.Port}\" required>"));
            left.Append(HtmlComponents.FormGroup(
                "endpoint", "Endpoint WebSocket",
                $"<input type=\"text\" id=\"endpoint\" name=\"endpoint\" value=\"{E(config.Endpoint)}\" required>"));
            left.Append("</div>");
            left.Append("<span class=\"field-hint\">Alterar a porta ou o endpoint reinicia o servidor do overlay - atualize a URL no Browser Source do OBS depois de salvar.</span>");

            left.Append(HtmlComponents.SectionTitle("Comportamento"));
            left.Append("<div class=\"form-group\"><label>Modo de Exibição</label>");
            left.Append("<div class=\"mode-options\">");
            left.Append("<label><input type=\"radio\" name=\"mode\" value=\"fadeout\"").Append(config.Mode == ChatOverlayMode.FadeOut ? " checked" : "").Append("> Transparente (fade out)</label>");
            left.Append("<label><input type=\"radio\" name=\"mode\" value=\"permanent\"").Append(config.Mode == ChatOverlayMode.Permanent ? " checked" : "").Append("> Permanente</label>");
            left.Append("</div></div>");

            left.Append("<div class=\"field-row\">");
            left.Append(HtmlComponents.FormGroup(
                "maxMessages", "Máx. de Mensagens na Tela",
                $"<input type=\"number\" id=\"maxMessages\" name=\"maxMessages\" min=\"1\" max=\"200\" value=\"{config.MaxMessages}\" required>"));
            left.Append(HtmlComponents.FormGroup(
                "fadeTimeMs", "Tempo de Fade (ms)",
                $"<input type=\"number\" id=\"fadeTimeMs\" name=\"fadeTimeMs\" min=\"1000\" step=\"500\" value=\"{config.FadeTimeMs}\" required>"));
            left.Append("</div>");

            left.Append(HtmlComponents.SectionTitle("Visual"));
            left.Append("<div class=\"field-row\">");
            left.Append(HtmlComponents.FormGroup(
                "emoteSize", "Tamanho dos Emotes (px)",
                $"<input type=\"number\" id=\"emoteSize\" name=\"emoteSize\" min=\"8\" max=\"128\" value=\"{config.EmoteSize}\" required>"));
            left.Append(HtmlComponents.FormGroup(
                "badgeSize", "Tamanho dos Badges (px)",
                $"<input type=\"number\" id=\"badgeSize\" name=\"badgeSize\" min=\"8\" max=\"64\" value=\"{config.BadgeSize}\" required>"));
            left.Append("</div>");

            left.Append("<div class=\"message-header\" style=\"margin-top: 16px;\">");
            left.Append("<label style=\"margin-bottom: 0;\">Exibir Badges (MOD/VIP/SUB/BR)</label>");
            left.Append(HtmlComponents.ToggleSwitch("showBadges", config.ShowBadges));
            left.Append("</div>");

            left.Append(HtmlComponents.SectionTitle("CSS Customizado"));
            left.Append(HtmlComponents.FormGroup(
                "customCss", "CSS Adicional (avançado)",
                $"<textarea id=\"customCss\" name=\"customCss\" rows=\"6\" placeholder=\".msg {{ ... }}\">{E(config.CustomCss)}</textarea>",
                hint: "Aplicado direto no overlay em tempo real. CSS inválido pode quebrar o layout."));

            // ---------- Coluna direita ----------
            var right = new StringBuilder();
            right.Append(HtmlComponents.SectionTitle("Preview ao Vivo"));
            right.Append("<div class=\"preview-wrapper\"><iframe id=\"previewFrame\" class=\"preview-frame\" src=\"/\"></iframe></div>");
            right.Append("<button type=\"button\" id=\"sendTestMsgBtn\" class=\"btn-secondary\">Enviar Mensagem de Teste</button>");

            // ---------- Corpo completo ----------
            var body = new StringBuilder();
            body.Append(HtmlComponents.TwoColumnGrid(left.ToString(), right.ToString(), stickyRight: true));
            body.Append(HtmlComponents.SubmitButton("Salvar Configurações"));
            body.Append("</form>");

            var options = new PageShellOptions
            {
                Title = "Configurações do Overlay de Chat - StreamerBot Unified Hub",
                LogoIcon = "💬",
                HeaderTitle = "Configurações do Overlay de Chat",
                HeaderSubtitle = "Ajuste conexão, comportamento e visual do overlay",
                Wide = true,
                Error = model.Error,
                Success = model.Saved ? "Configurações salvas com sucesso!" : null,
                ExtraCss = @"
                    .container { max-width: 520px; }
                    .mode-options { display: flex; gap: 10px; }
                    .mode-options label { flex: 1; display: flex; align-items: center; gap: 8px; background-color: var(--item-bg); border: 1px solid transparent; border-radius: 8px; padding: 10px 12px; cursor: pointer; font-size: 13px; }
                    .mode-options label:has(input:checked) { border-color: var(--primary); background-color: var(--item-bg-hover); }
                    .field-row { display: flex; gap: 12px; }
                    .field-row .form-group { flex: 1; }
                    .preview-wrapper { position: relative; flex: 1; min-height: 260px; border-radius: 8px; overflow: hidden; margin-bottom: 12px; background-image: linear-gradient(45deg, #2a2a2a 25%, transparent 25%), linear-gradient(-45deg, #2a2a2a 25%, transparent 25%), linear-gradient(45deg, transparent 75%, #2a2a2a 75%), linear-gradient(-45deg, transparent 75%, #2a2a2a 75%); background-size: 20px 20px; background-position: 0 0, 0 10px, 10px -10px, -10px 0px; background-color: #1a1a1a; }
                    .preview-frame { width: 100%; height: 100%; border: none; }
                    .btn-secondary { width: 100%; background-color: var(--item-bg); color: var(--text-main); border: 1px solid var(--border); padding: 12px; border-radius: 50px; font-size: 13px; font-weight: 700; cursor: pointer; margin-top: 8px; }
                    .btn-secondary:hover { background-color: var(--item-bg-hover); }
                ",
                ExtraScript = @"
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
                                    showBadges: document.querySelector('input[name=""showBadges""]').checked,
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
                "
            };

            return HtmlPageShell.Render(options, body.ToString());
        }
    }
}