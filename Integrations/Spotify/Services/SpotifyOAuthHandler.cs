using System.Reflection;
using System.Web;
using StreamerBot.UnifiedHub.Core.Abstractions;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyOAuthHandler(
        ILocalHttpServer httpServer,
        IBrowserService browserService,
        SpotifyAuthService spotifyAuthService,
        HttpClient httpClient)
    {
        private readonly ILocalHttpServer _httpServer = httpServer ?? throw new ArgumentNullException(nameof(httpServer));
        private readonly IBrowserService _browserService = browserService ?? throw new ArgumentNullException(nameof(browserService));
        private readonly SpotifyAuthService _spotifyAuthService = spotifyAuthService ?? throw new ArgumentNullException(nameof(spotifyAuthService));
        private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        public async Task<(string? clientId, string? clientSecret, string? refreshToken)> AuthenticateUserAsync(
            SpotifyConfig config,
            CancellationToken cancellationToken = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "A configuração do Spotify não pode ser nula.");

            string redirectUri = string.IsNullOrWhiteSpace(config.RedirectUri)
                ? "http://127.0.0.1:5000/callback/"
                : config.RedirectUri;

            _httpServer.Start(redirectUri);
            _browserService.OpenUrl(redirectUri);

            string clientIdSalvo = config.ClientId ?? string.Empty;
            string clientSecretSalvo = config.ClientSecret ?? string.Empty;

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                while (!linkedCts.Token.IsCancellationRequested)
                {
                    var waitForRequestTask = _httpServer.WaitForRequestAsync();
                    var completedTask = await Task.WhenAny(waitForRequestTask, Task.Delay(-1, linkedCts.Token));

                    if (completedTask != waitForRequestTask)
                    {
                        throw new TimeoutException("Tempo limite esgotado ou operação cancelada. O painel de configurações foi fechado antes da conclusão.");
                    }

                    var context = await waitForRequestTask ?? throw new OperationCanceledException("O servidor HTTP local foi interrompido.");
                    string rawUrl = context.RawUrl ?? string.Empty;

                    if (rawUrl.Contains("/cancel", StringComparison.OrdinalIgnoreCase))
                    {
                        string htmlCancel = @"
                            <html>
                            <body style='background:#121212; color:#fff; font-family:sans-serif; text-align:center; padding-top:50px;'>
                                <h2>Operação cancelada pelo usuário.</h2>
                                <p>Você já pode fechar esta aba e retornar ao aplicativo.</p>
                            </body>
                            </html>";

                        context.RespondHtml(htmlCancel, "text/html; charset=utf-8");
                        await Task.Delay(500, cancellationToken);
                        _httpServer.Stop();
                        throw new OperationCanceledException("O usuário cancelou a configuração do Spotify.");
                    }

                    if (context.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        var formData = HttpUtility.ParseQueryString(context.Body ?? string.Empty);
                        clientIdSalvo = formData["clientId"]?.Trim() ?? string.Empty;
                        clientSecretSalvo = formData["clientSecret"]?.Trim() ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(clientIdSalvo) || string.IsNullOrWhiteSpace(clientSecretSalvo))
                        {
                            string erroCampos = "Preencha o Client ID e o Client Secret antes de salvar.";
                            context.RespondHtml(RenderHtmlForm(clientIdSalvo, clientSecretSalvo, erroCampos), "text/html; charset=utf-8");
                            continue;
                        }

                        bool isValid = await IsClientIdValidAsync(clientIdSalvo, clientSecretSalvo, cancellationToken);
                        if (!isValid)
                        {
                            string erroClientId = "O Client ID informado é inválido ou não existe no Spotify Developer Dashboard.";
                            context.RespondHtml(RenderHtmlForm(clientIdSalvo, clientSecretSalvo, erroClientId), "text/html; charset=utf-8");
                            continue;
                        }

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
                    else if (context.RawUrl != null && context.RawUrl.Contains("code="))
                    {
                        var query = HttpUtility.ParseQueryString(new Uri("http://127.0.0.1" + context.RawUrl).Query);
                        string code = query["code"] ?? string.Empty;

                        try
                        {
                            string refreshToken = await SpotifyAuthService.ExchangeCodeForRefreshTokenAsync(
                                clientIdSalvo,
                                clientSecretSalvo,
                                code,
                                redirectUri
                            );

                            string htmlSucesso = @"
                                <html>
                                <head><title>Configurações Salvas!</title></head>
                                <body style='font-family:sans-serif; text-align:center; padding-top:50px; background:#121212; color:#1db954;'>
                                    <h2>Configurações do Spotify salvas com sucesso!</h2>
                                    <p>Sua conta foi vinculada ao bot. Você já pode fechar esta janela.</p>
                                </body>
                                </html>";

                            context.RespondHtml(htmlSucesso, "text/html; charset=utf-8");
                            return (clientIdSalvo, clientSecretSalvo, refreshToken);
                        }
                        catch (Exception ex)
                        {
                            string erroApi = $"Falha ao validar credenciais (Client Secret pode estar incorreto): {ex.Message}";
                            context.RespondHtml(RenderHtmlForm(clientIdSalvo, clientSecretSalvo, erroApi), "text/html; charset=utf-8");

                            await Task.Delay(500, cancellationToken);
                            throw new InvalidOperationException(erroApi, ex);
                        }
                    }
                    else
                    {
                        context.RespondHtml(RenderHtmlForm(clientIdSalvo, clientSecretSalvo, null), "text/html; charset=utf-8");
                    }
                }
            }
            finally
            {
                _httpServer.Stop();
            }

            throw new OperationCanceledException("O processo de configuração do Spotify foi cancelado.");
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

        private static string LoadEmbeddedResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using Stream? stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException($"Recurso embutido '{resourceName}' não encontrado.");
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }

        private async Task<bool> IsClientIdValidAsync(string clientId, string clientSecret, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
                var authHeader = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);

                request.Content = new FormUrlEncodedContent([
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                ]);

                var response = await _httpClient.SendAsync(request, cancellationToken);
                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                return !responseBody.Contains("invalid_client");
            }
            catch
            {
                return true;
            }
        }
    }
}