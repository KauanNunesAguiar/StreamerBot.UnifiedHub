using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using StreamerBot.UnifiedHub.Core.Abstractions;
using StreamerBot.UnifiedHub.Core.Compatibility;
using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Core.Services.OAuth
{
    public class OAuthFlowHandler(
        ILocalHttpServer httpServer,
        IBrowserService browserService,
        IOAuthFlowStrategy strategy)
    {
        private readonly ILocalHttpServer _httpServer = httpServer;
        private readonly IBrowserService _browserService = browserService;
        private readonly IOAuthFlowStrategy _strategy = strategy;
        private static readonly TimeSpan InactivityTimeout = TimeSpan.FromMinutes(10);

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

            OAuthResult? pendingResult = null;
            bool awaitingPostAuthSubmission = false;

            using var timeoutCts = new CancellationTokenSource();
            using var inactivityTimer = new Timer(_ => timeoutCts.Cancel(), null, InactivityTimeout, Timeout.InfiniteTimeSpan);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                while (!linkedCts.Token.IsCancellationRequested)
                {
                    // Passa o linkedCts.Token diretamente para evitar orphaned tasks
                    var context = await _httpServer.WaitForRequestAsync(linkedCts.Token) ?? throw new OperationCanceledException("Servidor encerrado.");

                    inactivityTimer.Change(InactivityTimeout, Timeout.InfiniteTimeSpan);

                    string rawUrl = context.RawUrl ?? string.Empty;

                    // 1. Rota de Cancelamento
                    if (rawUrl.Contains("/cancel", StringComparison.OrdinalIgnoreCase))
                    {
                        string htmlCancel = awaitingPostAuthSubmission
                            ? "<html><body style='background:#121212; color:#fff; font-family:sans-serif; text-align:center; padding-top:50px;'><h2>Configurações extras não foram salvas.</h2><p>Seu login continua válido. Você já pode fechar esta aba.</p></body></html>"
                            : "<html><body style='background:#121212; color:#fff; font-family:sans-serif; text-align:center; padding-top:50px;'><h2>Operação cancelada pelo usuário.</h2><p>Você já pode fechar esta aba e retornar ao aplicativo.</p></body></html>";

                        context.RespondHtml(htmlCancel, "text/html; charset=utf-8");
                        await Task.Delay(500, cancellationToken);

                        if (awaitingPostAuthSubmission && pendingResult != null)
                            return pendingResult;

                        throw new OperationCanceledException("O usuário cancelou a configuração.");
                    }

                    // 2. Método POST (Formulários)
                    if (context.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        if (awaitingPostAuthSubmission && pendingResult != null)
                        {
                            string? error = await _strategy.ProcessPostAuthStepAsync(pendingResult, context.Body ?? string.Empty, linkedCts.Token);
                            if (error != null)
                            {
                                string retryHtml = await _strategy.RenderPostAuthStepHtmlAsync(pendingResult, error, linkedCts.Token);
                                context.RespondHtml(retryHtml, "text/html; charset=utf-8");
                                continue;
                            }

                            context.RespondHtml(BuildFinalSuccessHtml(), "text/html; charset=utf-8");
                            await Task.Delay(500, cancellationToken);
                            return pendingResult;
                        }

                        var formData = HttpUtility.ParseQueryString(context.Body ?? string.Empty);
                        clientIdSalvo = formData["clientId"]?.Trim() ?? string.Empty;
                        clientSecretSalvo = formData["clientSecret"]?.Trim() ?? string.Empty;

                        if (string.IsNullOrWhiteSpace(clientIdSalvo) || string.IsNullOrWhiteSpace(clientSecretSalvo))
                        {
                            context.RespondHtml(await _strategy.RenderFormHtml(clientIdSalvo, clientSecretSalvo, "Preencha o Client ID e o Client Secret."), "text/html; charset=utf-8");
                            continue;
                        }

                        bool isValid = await _strategy.ValidateCredentialsAsync(clientIdSalvo, clientSecretSalvo, linkedCts.Token);
                        if (!isValid)
                        {
                            context.RespondHtml(await _strategy.RenderFormHtml(clientIdSalvo, clientSecretSalvo, _strategy.InvalidCredentialsMessage), "text/html; charset=utf-8");
                            continue;
                        }

                        string authUrl = _strategy.BuildAuthorizationUrl(clientIdSalvo, redirectUri);
                        context.Redirect(authUrl);
                    }
                    // 3. Callback OAuth (Code Authorization)
                    else if (rawUrl.Contains("code="))
                    {
                        var query = HttpUtility.ParseQueryString(new Uri("http://127.0.0.1" + rawUrl).Query);
                        string code = query["code"] ?? string.Empty;

                        try
                        {
                            string refreshToken = await _strategy.ExchangeCodeForRefreshTokenAsync(clientIdSalvo, clientSecretSalvo, code, redirectUri);
                            var result = new OAuthResult
                            {
                                ClientId = clientIdSalvo,
                                ClientSecret = clientSecretSalvo,
                                RefreshToken = refreshToken
                            };

                            if (_strategy.HasPostAuthStep)
                            {
                                pendingResult = result;
                                awaitingPostAuthSubmission = true;
                                string setupHtml = await _strategy.RenderPostAuthStepHtmlAsync(result, null, linkedCts.Token);
                                context.RespondHtml(setupHtml, "text/html; charset=utf-8");
                                continue;
                            }

                            context.RespondHtml(BuildFinalSuccessHtml(), "text/html; charset=utf-8");
                            return result;
                        }
                        catch (Exception ex)
                        {
                            string erroApi = _strategy.BuildExchangeErrorMessage(ex);
                            context.RespondHtml(await _strategy.RenderFormHtml(clientIdSalvo, clientSecretSalvo, erroApi), "text/html; charset=utf-8");
                            await Task.Delay(500, cancellationToken);
                            throw new InvalidOperationException(erroApi, ex);
                        }
                    }
                    // 4. Renderização Padrão (GET inicial)
                    else
                    {
                        string html = awaitingPostAuthSubmission && pendingResult != null
                            ? await _strategy.RenderPostAuthStepHtmlAsync(pendingResult, null, linkedCts.Token)
                            : await _strategy.RenderFormHtml(clientIdSalvo, clientSecretSalvo, null);

                        context.RespondHtml(html, "text/html; charset=utf-8");
                    }
                }
            }
            finally
            {
                _httpServer.Stop();
            }

            throw new OperationCanceledException("O processo de configuração foi cancelado.");
        }

        private static string BuildFinalSuccessHtml() => @"
            <html>
            <head><title>Configurações Salvas!</title></head>
            <body style='font-family:sans-serif; text-align:center; padding-top:50px; background:#121212; color:#1db954;'>
                <h2>Configurações salvas com sucesso!</h2>
                <p>Sua conta foi vinculada ao bot. Você já pode fechar esta janela.</p>
            </body>
            </html>";
    }
}