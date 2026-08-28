using System.Net;
using System.Text;
using StreamerBot.UnifiedHub.Core.Models;
using StreamerBot.UnifiedHub.Core.Services.Html;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    /// <summary>
    /// Gera o HTML das telas de login/configurações do Spotify manualmente, sem RazorLight.
    /// Motivo: o RazorLight escaneia todas as assemblies do AppDomain para montar as
    /// referências de compilação, e quebra quando encontra uma assembly dinâmica sem
    /// Location (como o código C# compilado em memória pelo próprio Streamer.bot).
    /// Isso é uma limitação conhecida e não configurável do RazorLight nesse cenário.
    /// </summary>
    public static class SpotifyHtmlTemplates
    {
        private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        public static string RenderLogin(OAuthLoginViewModel model)
        {
            var body = new StringBuilder();

            body.Append("<form method=\"POST\">");

            body.Append(HtmlComponents.FormGroup(
                "clientId", "Client ID",
                $"<input type=\"text\" id=\"clientId\" name=\"clientId\" value=\"{E(model.ClientId)}\" placeholder=\"Insira o Client ID\" required autocomplete=\"off\">"));

            body.Append(HtmlComponents.FormGroup(
                "clientSecret", "Client Secret",
                $"<input type=\"password\" id=\"clientSecret\" name=\"clientSecret\" value=\"{E(model.ClientSecret)}\" placeholder=\"Insira o Client Secret\" required autocomplete=\"off\">",
                hint: "Obtenha estas chaves no Spotify Developer Dashboard."));

            body.Append(HtmlComponents.SubmitButton("Salvar e Conectar"));
            body.Append("</form>");

            body.Append(HtmlComponents.CancelLink("cancel", "Cancelar e voltar ao aplicativo"));

            body.Append("<div class=\"footer-note\">Certifique-se de que a <strong>Redirect URI</strong> no Dashboard do Spotify seja:<br><code>http://127.0.0.1:5000/callback/</code></div>");

            var options = new PageShellOptions
            {
                Title = "Configurações do Spotify - StreamerBot Unified Hub",
                LogoIcon = "🎵",
                HeaderTitle = "Configurações do Spotify",
                HeaderSubtitle = "Integração StreamerBot Unified Hub",
                Error = model.Error,
                ExtraCss = @"
                    .container { max-width: 480px; }
                    .form-group label { letter-spacing: 0.5px; text-transform: uppercase; color: var(--text-sub); }
                    .form-group input { padding: 12px 14px; border: 1px solid transparent; font-size: 14px; }
                    .form-group input:focus { background-color: #333333; }
                    .footer-note { margin-top: 24px; padding-top: 16px; border-top: 1px solid var(--border); text-align: center; font-size: 12px; color: var(--text-sub); line-height: 1.4; }
                ",
                ExtraScript = @"
                    document.querySelector('form').addEventListener('submit', function (e) {
                        var a = document.getElementById('clientId'), b = document.getElementById('clientSecret');
                        if (a) a.value = a.value.trim();
                        if (b) b.value = b.value.trim();
                    });
                "
            };

            return HtmlPageShell.Render(options, body.ToString());
        }

        public static string RenderSettings(SpotifySettingsViewModel model)
        {
            // ---------- Coluna esquerda ----------
            var left = new StringBuilder();

            left.Append(HtmlComponents.SectionTitle("Playlist de Lives"));
            left.Append("<div class=\"playlist-list\">");

            if (model.Playlists.Count == 0)
            {
                left.Append("<p class=\"empty-state\">Nenhuma playlist encontrada na sua conta.</p>");
            }
            else
            {
                foreach (var playlist in model.Playlists)
                {
                    bool selected = playlist.Id == model.SelectedPlaylistId;
                    left.Append("<label class=\"playlist-item\"><input type=\"radio\" name=\"playlistId\" value=\"").Append(E(playlist.Id)).Append("\"").Append(selected ? " checked" : "").Append(">");
                    if (string.IsNullOrEmpty(playlist.ImageUrl))
                        left.Append("<div class=\"playlist-thumb-placeholder\">🎵</div>");
                    else
                        left.Append("<img class=\"playlist-thumb\" src=\"").Append(E(playlist.ImageUrl)).Append("\" alt=\"\">");
                    left.Append("<div class=\"playlist-info\"><span class=\"playlist-name\">").Append(E(playlist.Name)).Append("</span>");
                    left.Append("<span class=\"playlist-tracks\">").Append(playlist.TracksTotal).Append(" faixas</span></div></label>");
                }
            }
            left.Append("</div>");

            left.Append(HtmlComponents.SectionTitle("Regras de Reprodução"));
            left.Append(HtmlComponents.FormGroup(
                "voteSkipThreshold", "Votos para Pular Música (VoteSkip)",
                $"<input type=\"number\" id=\"voteSkipThreshold\" name=\"voteSkipThreshold\" min=\"1\" max=\"100\" value=\"{model.VoteSkipThreshold}\">"));
            left.Append(HtmlComponents.FormGroup(
                "queueSize", "Tamanho da Fila Exibida",
                $"<input type=\"number\" id=\"queueSize\" name=\"queueSize\" min=\"1\" max=\"50\" value=\"{model.QueueSize}\">"));
            left.Append(HtmlComponents.FormGroup(
                "pollingIntervalMs", "Intervalo de Monitoramento (ms)",
                $"<input type=\"number\" id=\"pollingIntervalMs\" name=\"pollingIntervalMs\" min=\"1000\" step=\"500\" value=\"{model.PollingIntervalMs}\">",
                hint: "Frequência com que o bot verifica a música tocando. Padrão: 5000ms."));

            left.Append(HtmlComponents.SectionTitle("Identidade do Bot"));
            left.Append(HtmlComponents.FormGroup(
                "BotLabel", "Nome do Bot no Chat",
                $"<input type=\"text\" id=\"BotLabel\" name=\"BotLabel\" value=\"{E(model.BotLabel)}\">"));

            // ---------- Coluna direita ----------
            var right = new StringBuilder();
            right.Append(HtmlComponents.SectionTitle("Mensagens do Chat"));
            right.Append("<div class=\"messages-container\">");

            foreach (var msg in model.Messages)
            {
                right.Append("<div class=\"message-group\"><div class=\"message-header\">");
                right.Append("<label for=\"msg_").Append(E(msg.Definition.Key)).Append("\" style=\"margin-bottom: 0;\">").Append(E(msg.Definition.Label)).Append("</label>");
                right.Append(HtmlComponents.ToggleSwitch(
                    $"msgEnabled_{msg.Definition.Key}",
                    msg.Definition.Enabled,
                    onChangeJs: "this.closest('.toggle-switch').querySelector('.toggle-label').textContent = this.checked ? 'Habilitado' : 'Desabilitado'"));
                right.Append("</div>");
                right.Append("<textarea id=\"msg_").Append(E(msg.Definition.Key)).Append("\" name=\"msg_").Append(E(msg.Definition.Key)).Append("\" rows=\"2\" placeholder=\"").Append(E(msg.Definition.Description)).Append("\">").Append(E(msg.Value)).Append("</textarea>");
                right.Append("<div class=\"field-hint\">Variáveis disponíveis: ");
                foreach (var placeholder in msg.Definition.Placeholders)
                    right.Append("<code>").Append(E(placeholder)).Append("</code> ");
                right.Append("</div></div>");
            }
            right.Append("</div>");

            // ---------- Corpo completo ----------
            var body = new StringBuilder();
            body.Append("<div class=\"step-indicator\">Passo 2 de 2</div>");
            body.Append("<form method=\"POST\">");
            body.Append(HtmlComponents.TwoColumnGrid(left.ToString(), right.ToString()));
            body.Append(HtmlComponents.SubmitButton("Salvar e Concluir"));
            body.Append("</form>");
            body.Append(HtmlComponents.CancelLink("cancel", "Pular esta etapa"));

            var options = new PageShellOptions
            {
                Title = "Configurações do Spotify - StreamerBot Unified Hub",
                LogoIcon = "🎵",
                HeaderTitle = "Configurações do Spotify",
                HeaderSubtitle = "Escolha a playlist das lives e personalize as respostas no chat",
                Wide = true,
                Error = model.Error,
                ExtraCss = @"
            .container { max-width: 650px; }
            .step-indicator { text-align: center; font-size: 12px; font-weight: 700; letter-spacing: 0.5px; color: var(--primary); text-transform: uppercase; margin-bottom: 10px; }
            .playlist-list { max-height: 180px; overflow-y: auto; display: flex; flex-direction: column; gap: 8px; margin-bottom: 20px; padding-right: 4px; }
            .playlist-item { display: flex; align-items: center; gap: 12px; background-color: var(--item-bg); border: 1px solid transparent; border-radius: 8px; padding: 10px 12px; cursor: pointer; }
            .playlist-item:hover { background-color: var(--item-bg-hover); }
            .playlist-item input[type='radio'] { accent-color: var(--primary); width: 16px; height: 16px; flex-shrink: 0; }
            .playlist-item:has(input:checked) { border-color: var(--primary); background-color: var(--item-bg-hover); }
            .playlist-thumb, .playlist-thumb-placeholder { width: 44px; height: 44px; border-radius: 6px; object-fit: cover; flex-shrink: 0; }
            .playlist-thumb-placeholder { background-color: #282828; display: flex; align-items: center; justify-content: center; font-size: 18px; }
            .playlist-info { display: flex; flex-direction: column; min-width: 0; }
            .playlist-name { font-size: 14px; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
            .playlist-tracks { font-size: 12px; color: var(--text-sub); }
            .messages-container { flex: 1; min-height: 0; overflow-y: auto; padding-right: 6px; margin-bottom: 15px; }
            .message-group { margin-bottom: 12px; background-color: var(--item-bg); border-radius: 8px; padding: 12px; }
            .message-group textarea { resize: vertical; }
        "
            };

            return HtmlPageShell.Render(options, body.ToString());
        }
    }
}