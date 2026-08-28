using System.Net;
using System.Text;
using StreamerBot.UnifiedHub.Core.Models;
using StreamerBot.UnifiedHub.Core.Services;
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

        private static readonly string SharedCss = EmbeddedResourceReader.ReadText("Core.Assets.SharedStyles.css");
        private static readonly string ChatCss = EmbeddedResourceReader.ReadText("Core.Assets.ChatSettingsStyles.css");

        public static string RenderLogin(OAuthLoginViewModel model)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang=\"pt-BR\"><head><meta charset=\"UTF-8\">");
            sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.Append("<title>Configurações do Spotify - StreamerBot Unified Hub</title>");
            sb.Append("<style>").Append(SharedCss).Append(ChatCss);
            sb.Append(@"
                .container { max-width: 480px; }
                .form-group label { letter-spacing: 0.5px; text-transform: uppercase; color: var(--text-sub); }
                .form-group input { padding: 12px 14px; border: 1px solid transparent; font-size: 14px; }
                .form-group input:focus { background-color: #333333; }
                .footer-note { margin-top: 24px; padding-top: 16px; border-top: 1px solid var(--border); text-align: center; font-size: 12px; color: var(--text-sub); line-height: 1.4; }
            ");
            sb.Append("</style></head><body><div class=\"container wide\">");
            sb.Append("<div class=\"header\"><div class=\"logo-icon\">🎵</div><h1>Configurações do Spotify</h1><p>Integração StreamerBot Unified Hub</p></div>");

            if (!string.IsNullOrEmpty(model.Error))
                sb.Append("<div class=\"error\">").Append(E(model.Error)).Append("</div>");

            sb.Append("<form method=\"POST\">");
            sb.Append("<div class=\"settings-grid\">");
            sb.Append("<div class=\"col\">");
            sb.Append("<div class=\"form-group\"><label for=\"clientId\">Client ID</label>");
            sb.Append("<input type=\"text\" id=\"clientId\" name=\"clientId\" value=\"").Append(E(model.ClientId)).Append("\" placeholder=\"Insira o Client ID\" required autocomplete=\"off\"></div>");
            sb.Append("<div class=\"form-group\"><label for=\"clientSecret\">Client Secret</label>");
            sb.Append("<input type=\"password\" id=\"clientSecret\" name=\"clientSecret\" value=\"").Append(E(model.ClientSecret)).Append("\" placeholder=\"Insira o Client Secret\" required autocomplete=\"off\">");
            sb.Append("<span class=\"field-hint\">Obtenha estas chaves no Spotify Developer Dashboard.</span></div>");
            sb.Append("<button type=\"submit\" class=\"btn-submit\">Salvar e Conectar</button></form>");
            sb.Append("<div style=\"text-align: center; margin-top: 15px;\"><a href=\"cancel\" style=\"color: var(--text-sub); text-decoration: none; font-size: 13px;\">Cancelar e voltar ao aplicativo</a></div>");
            sb.Append("<div class=\"footer-note\">Certifique-se de que a <strong>Redirect URI</strong> no Dashboard do Spotify seja:<br><code>http://127.0.0.1:5000/callback/</code></div>");
            sb.Append("</div>");
            sb.Append(@"<script>
                document.querySelector('form').addEventListener('submit', function (e) {
                    var a = document.getElementById('clientId'), b = document.getElementById('clientSecret');
                    if (a) a.value = a.value.trim();
                    if (b) b.value = b.value.trim();
                });
            </script>");
            sb.Append("</body></html>");
            return sb.ToString();
        }

        public static string RenderSettings(SpotifySettingsViewModel model)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html lang=\"pt-BR\"><head><meta charset=\"UTF-8\">");
            sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.Append("<title>Configurações do Spotify - StreamerBot Unified Hub</title>");
            sb.Append("<style>").Append(SharedCss).Append(ChatCss);
            sb.Append(@"
                .container { max-width: 650px; }
                .step-indicator { text-align: center; font-size: 12px; font-weight: 700; letter-spacing: 0.5px; color: var(--primary); text-transform: uppercase; margin-bottom: 10px; }
                .section-title { font-size: 13px; font-weight: 700; letter-spacing: 0.5px; text-transform: uppercase; color: var(--primary); margin: 20px 0 12px 0; border-bottom: 1px solid var(--border); padding-bottom: 6px; }
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
            ");
            sb.Append("</style></head><body><div class=\"container wide\">");
            sb.Append("<div class=\"step-indicator\">Passo 2 de 2</div>");
            sb.Append("<div class=\"header\"><div class=\"logo-icon\">🎵</div><h1>Configurações do Spotify</h1><p>Escolha a playlist das lives e personalize as respostas no chat</p></div>");

            if (!string.IsNullOrEmpty(model.Error))
                sb.Append("<div class=\"error\">").Append(E(model.Error)).Append("</div>");

            sb.Append("<form method=\"POST\">");
            sb.Append("<div class=\"settings-grid\">");
            sb.Append("<div class=\"col\">");
            sb.Append("<div class=\"section-title\">Playlist de Lives</div><div class=\"playlist-list\">");

            if (model.Playlists.Count == 0)
            {
                sb.Append("<p class=\"empty-state\">Nenhuma playlist encontrada na sua conta.</p>");
            }
            else
            {
                foreach (var playlist in model.Playlists)
                {
                    bool selected = playlist.Id == model.SelectedPlaylistId;
                    sb.Append("<label class=\"playlist-item\"><input type=\"radio\" name=\"playlistId\" value=\"").Append(E(playlist.Id)).Append("\"").Append(selected ? " checked" : "").Append(">");
                    if (string.IsNullOrEmpty(playlist.ImageUrl))
                        sb.Append("<div class=\"playlist-thumb-placeholder\">🎵</div>");
                    else
                        sb.Append("<img class=\"playlist-thumb\" src=\"").Append(E(playlist.ImageUrl)).Append("\" alt=\"\">");
                    sb.Append("<div class=\"playlist-info\"><span class=\"playlist-name\">").Append(E(playlist.Name)).Append("</span>");
                    sb.Append("<span class=\"playlist-tracks\">").Append(playlist.TracksTotal).Append(" faixas</span></div></label>");
                }
            }
            sb.Append("</div>");

            sb.Append("<div class=\"section-title\">Regras de Reprodução</div>");
            sb.Append("<div class=\"form-group\"><label for=\"voteSkipThreshold\">Votos para Pular Música (VoteSkip)</label>");
            sb.Append("<input type=\"number\" id=\"voteSkipThreshold\" name=\"voteSkipThreshold\" min=\"1\" max=\"100\" value=\"").Append(model.VoteSkipThreshold).Append("\"></div>");
            sb.Append("<div class=\"form-group\"><label for=\"queueSize\">Tamanho da Fila Exibida</label>");
            sb.Append("<input type=\"number\" id=\"queueSize\" name=\"queueSize\" min=\"1\" max=\"50\" value=\"").Append(model.QueueSize).Append("\"></div>");
            sb.Append("<div class=\"form-group\"><label for=\"pollingIntervalMs\">Intervalo de Monitoramento (ms)</label>");
            sb.Append("<input type=\"number\" id=\"pollingIntervalMs\" name=\"pollingIntervalMs\" min=\"1000\" step=\"500\" value=\"").Append(model.PollingIntervalMs).Append("\">");
            sb.Append("<span class=\"field-hint\">Frequência com que o bot verifica a música tocando. Padrão: 5000ms.</span></div>");

            sb.Append("<div class=\"section-title\">Identidade do Bot</div>");
            sb.Append("<div class=\"form-group\"><label for=\"BotLabel\">Nome do Bot no Chat</label>");
            sb.Append("<input type=\"text\" id=\"BotLabel\" name=\"BotLabel\" value=\"").Append(E(model.BotLabel)).Append("\"></div>");

            sb.Append("</div>");
            sb.Append("<div class=\"col\">");

            sb.Append("<div class=\"section-title\">Mensagens do Chat</div><div class=\"messages-container\">");
            foreach (var msg in model.Messages)
            {
                sb.Append("<div class=\"message-group\"><div class=\"message-header\">");
                sb.Append("<label for=\"msg_").Append(E(msg.Definition.Key)).Append("\" style=\"margin-bottom: 0;\">").Append(E(msg.Definition.Label)).Append("</label>");
                sb.Append("<label class=\"toggle-switch\" onclick=\"event.stopPropagation()\">");
                sb.Append("<input type=\"checkbox\" name=\"msgEnabled_").Append(E(msg.Definition.Key)).Append("\"").Append(msg.Definition.Enabled ? " checked" : "");
                sb.Append(" onchange=\"this.closest('.toggle-switch').querySelector('.toggle-label').textContent = this.checked ? 'Habilitado' : 'Desabilitado'\">");
                sb.Append("<span class=\"toggle-track\"></span><span class=\"toggle-label\">").Append(msg.Definition.Enabled ? "Habilitado" : "Desabilitado").Append("</span></label></div>");
                sb.Append("<textarea id=\"msg_").Append(E(msg.Definition.Key)).Append("\" name=\"msg_").Append(E(msg.Definition.Key)).Append("\" rows=\"2\" placeholder=\"").Append(E(msg.Definition.Description)).Append("\">").Append(E(msg.Value)).Append("</textarea>");
                sb.Append("<div class=\"field-hint\">Variáveis disponíveis: ");
                foreach (var placeholder in msg.Definition.Placeholders)
                    sb.Append("<code>").Append(E(placeholder)).Append("</code> ");
                sb.Append("</div></div>");
            }
            sb.Append("</div>"); // fecha .col direita
            sb.Append("</div>"); // fecha .settings-grid
            sb.Append("</div>");

            sb.Append("<button type=\"submit\" class=\"btn-submit\">Salvar e Concluir</button></form>");
            sb.Append("<div style=\"text-align: center; margin-top: 15px;\"><a href=\"cancel\" style=\"color: var(--text-sub); text-decoration: none; font-size: 13px;\">Pular esta etapa</a></div>");
            sb.Append("</div></body></html>");
            return sb.ToString();
        }
    }
}