using System.Web;
using StreamerBot.UnifiedHub.Core.Abstractions;
using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Core.Services
{
    public class OAuthFlowHandler(
        ILocalHttpServer httpServer,
        IBrowserService browserService,
        IOAuthFlowStrategy strategy)
    {
        private readonly ILocalHttpServer _httpServer = httpServer;
        private readonly IBrowserService _browserService = browserService;
        private readonly IOAuthFlowStrategy _strategy = strategy;

        public async Task<OAuthResult> RunAsync(
            string clientId,
            string clientSecret,
            string redirectUri,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(redirectUri))
                redirectUri = "http://127.0.0.1:5000/callback/";

            _httpServer.Start(redirectUri);
            _browserService.OpenUrl(redirectUri);

            string clientIdSalvo = clientId ?? string.Empty;
            string clientSecretSalvo = clientSecret ?? string.Empty;

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
                        await Task.Delay(500);
                        _httpServer.Stop();
                        throw new OperationCanceledException("O usuário cancelou a configuração.");
                    }

                    if (context.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        var formData = HttpUtility.ParseQueryString(context.Body ?? string.Empty);
                        clientIdSalvo = formData["clientId"]?.Trim() ?? string.Empty;
                        clientSecretSalvo = formData["clientSecret"]?.Trim() ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(clientIdSalvo) || string.IsNullOrWhiteSpace(clientSecretSalvo))
                        {
                            string erroCampos = "Preencha o Client ID e o Client Secret antes de salvar.";
                            context.RespondHtml(_strategy.RenderFormHtml(clientIdSalvo, clientSecretSalvo, erroCampos), "text/html; charset=utf-8");
                            continue;
                        }

                        bool isValid = await _strategy.ValidateCredentialsAsync(clientIdSalvo, clientSecretSalvo);
                        if (!isValid)
                        {
                            context.RespondHtml(_strategy.RenderFormHtml(clientIdSalvo, clientSecretSalvo, _strategy.InvalidCredentialsMessage), "text/html; charset=utf-8");
                            continue;
                        }

                        string authUrl = _strategy.BuildAuthorizationUrl(clientIdSalvo, redirectUri);
                        context.Redirect(authUrl);
                    }
                    else if (context.RawUrl != null && context.RawUrl.Contains("code="))
                    {
                        var query = HttpUtility.ParseQueryString(new Uri("http://127.0.0.1" + context.RawUrl).Query);
                        string code = query["code"] ?? string.Empty;

                        try
                        {
                            string refreshToken = await _strategy.ExchangeCodeForRefreshTokenAsync(
                                clientIdSalvo, clientSecretSalvo, code, redirectUri);

                            string htmlSucesso = @"
                                <html>
                                <head><title>Configurações Salvas!</title></head>
                                <body style='font-family:sans-serif; text-align:center; padding-top:50px; background:#121212; color:#1db954;'>
                                    <h2>Configurações salvas com sucesso!</h2>
                                    <p>Sua conta foi vinculada ao bot. Você já pode fechar esta janela.</p>
                                </body>
                                </html>";

                            context.RespondHtml(htmlSucesso, "text/html; charset=utf-8");
                            return new OAuthResult
                            {
                                ClientId = clientIdSalvo,
                                ClientSecret = clientSecretSalvo,
                                RefreshToken = refreshToken
                            };
                        }
                        catch (Exception ex)
                        {
                            string erroApi = _strategy.BuildExchangeErrorMessage(ex);
                            context.RespondHtml(_strategy.RenderFormHtml(clientIdSalvo, clientSecretSalvo, erroApi), "text/html; charset=utf-8");

                            await Task.Delay(500, cancellationToken);
                            throw new InvalidOperationException(erroApi, ex);
                        }
                    }
                    else
                    {
                        context.RespondHtml(_strategy.RenderFormHtml(clientIdSalvo, clientSecretSalvo, null), "text/html; charset=utf-8");
                    }
                }
            }
            finally
            {
                _httpServer.Stop();
            }

            throw new OperationCanceledException("O processo de configuração foi cancelado.");
        }
    }
}