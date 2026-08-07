using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using SpotifyAPI.Web;
using StreamerBot.UnifiedHub.Core.Abstractions;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyOAuthHandler
    {
        private readonly ILocalHttpServer _httpServer;
        private readonly IBrowserService _browserService;
        private readonly SpotifyService _spotifyService;

        public SpotifyOAuthHandler(
            ILocalHttpServer httpServer,
            IBrowserService browserService,
            SpotifyService spotifyService)
        {
            _httpServer = httpServer;
            _browserService = browserService;
            _spotifyService = spotifyService;
        }

        public async Task<(string? clientId, string? clientSecret, string? refreshToken)> AuthenticateUserAsync(
            string redirectUri,
            string initialClientId = "",
            string initialClientSecret = "")
        {
            _httpServer.Start(redirectUri);
            _browserService.OpenUrl(redirectUri);

            string clientIdSalvo = initialClientId;
            string clientSecretSalvo = initialClientSecret;
            string? erroMensagem = null;

            try
            {
                while (true)
                {
                    var context = await _httpServer.WaitForRequestAsync();
                    if (context == null) break;

                    if (context.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        var formData = HttpUtility.ParseQueryString(context.Body ?? string.Empty);
                        clientIdSalvo = formData["clientId"]?.Trim() ?? string.Empty;
                        clientSecretSalvo = formData["clientSecret"]?.Trim() ?? string.Empty;

                        string scopes = Uri.EscapeDataString(
                            "user-read-currently-playing " +
                            "user-read-playback-state " +
                            "user-modify-playback-state " +
                            "user-read-recently-played " +
                            "user-library-modify " +
                            "user-library-read " +
                            "playlist-read-private " +
                            "playlist-read-collaborative " +
                            "playlist-modify-public " +
                            "playlist-modify-private"
                        );

                        string authUrl = $"https://accounts.spotify.com/authorize?response_type=code&client_id={clientIdSalvo}&scope={scopes}&redirect_uri={Uri.EscapeDataString(redirectUri)}";
                        context.Redirect(authUrl);
                    }
                    else if (context.RawUrl != null && context.RawUrl.Contains("error="))
                    {
                        var query = HttpUtility.ParseQueryString(new Uri("http://127.0.0.1" + context.RawUrl).Query);
                        string erroSpotify = query["error"] ?? "Erro desconhecido";

                        erroMensagem = erroSpotify.Contains("invalid_client")
                            ? "Client ID inválido ou não encontrado no Spotify Developer Dashboard."
                            : $"Erro retornado pelo Spotify: {erroSpotify}";

                        context.RespondHtml(RenderHtmlForm(clientIdSalvo, clientSecretSalvo, erroMensagem), "text/html; charset=utf-8");
                    }
                    else if (context.RawUrl != null && context.RawUrl.Contains("code="))
                    {
                        var query = HttpUtility.ParseQueryString(new Uri("http://127.0.0.1" + context.RawUrl).Query);
                        string code = query["code"] ?? string.Empty;

                        try
                        {
                            string refreshToken = await _spotifyService.ExchangeCodeForRefreshTokenAsync(
                                clientIdSalvo,
                                clientSecretSalvo,
                                code,
                                redirectUri
                            );

                            string htmlSucesso = "<html><body style='background:#121212;color:#1db954;font-family:sans-serif;text-align:center;padding-top:50px;'><h2>Autenticação concluída com sucesso!</h2><p>Credenciais validadas e salvas. Pode fechar esta janela.</p></body></html>";
                            context.RespondHtml(htmlSucesso, "text/html; charset=utf-8");

                            return (clientIdSalvo, clientSecretSalvo, refreshToken);
                        }
                        catch (APIException ex)
                        {
                            erroMensagem = $"Falha na autenticação: {ex.Message}. Verifique se o Client Secret está correto.";
                            context.RespondHtml(RenderHtmlForm(clientIdSalvo, clientSecretSalvo, erroMensagem), "text/html; charset=utf-8");
                        }
                        catch (Exception ex)
                        {
                            erroMensagem = $"Erro ao validar token: {ex.Message}";
                            context.RespondHtml(RenderHtmlForm(clientIdSalvo, clientSecretSalvo, erroMensagem), "text/html; charset=utf-8");
                        }
                    }
                    else
                    {
                        context.RespondHtml(RenderHtmlForm(clientIdSalvo, clientSecretSalvo, erroMensagem), "text/html; charset=utf-8");
                    }
                }
            }
            finally
            {
                _httpServer.Stop();
            }

            return (null, null, null);
        }

        private string RenderHtmlForm(string clientId, string clientSecret, string? erro)
        {
            string htmlTemplate = LoadEmbeddedResource("StreamerBot.UnifiedHub.Integrations.Spotify.Assets.spotify-login.html");

            string divErro = string.IsNullOrEmpty(erro)
                ? string.Empty
                : $"<div class=\"error\">{erro}</div>";

            return htmlTemplate
                .Replace("{{ERROR_SECTION}}", divErro)
                .Replace("{{CLIENT_ID}}", clientId)
                .Replace("{{CLIENT_SECRET}}", clientSecret);
        }

        private string LoadEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException($"Recurso embutido '{resourceName}' não encontrado.");

                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}