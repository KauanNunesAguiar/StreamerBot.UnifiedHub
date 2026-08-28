using System;
using System.Collections.Generic;
using System.Text;
using StreamerBot.UnifiedHub.Core.Abstractions;
using StreamerBot.UnifiedHub.Core.Compatibility;
using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Core.Services.OAuth
{
    /// <summary>
    /// Fluxo leve que abre o navegador direto na tela de configurações pós-auth
    /// (RenderPostAuthStepHtmlAsync/ProcessPostAuthStepAsync da strategy), sem repetir
    /// login/troca de token. Útil pra reconfigurar playlist/mensagens sem OAuth de novo.
    /// </summary>
    public class SettingsOnlyFlowHandler(
        ILocalHttpServer httpServer,
        IBrowserService browserService,
        IOAuthFlowStrategy strategy)
    {
        private static readonly TimeSpan InactivityTimeout = TimeSpan.FromMinutes(10);

        public async Task<OAuthResult?> RunAsync(OAuthResult current, string redirectUri, CancellationToken cancellationToken = default)
        {
            if (!strategy.HasPostAuthStep)
                throw new InvalidOperationException("Esta integração não possui uma tela de configurações separada do login.");

            if (string.IsNullOrWhiteSpace(redirectUri))
                redirectUri = "http://127.0.0.1:5000/callback/";

            httpServer.Start(redirectUri);
            browserService.OpenUrl(redirectUri);

            using var timeoutCts = new CancellationTokenSource();
            using var inactivityTimer = new Timer(_ => timeoutCts.Cancel(), null, InactivityTimeout, Timeout.InfiniteTimeSpan);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                while (!linkedCts.Token.IsCancellationRequested)
                {
                    var context = await httpServer.WaitForRequestAsync(linkedCts.Token) ?? throw new OperationCanceledException("Servidor encerrado.");
                    inactivityTimer.Change(InactivityTimeout, Timeout.InfiniteTimeSpan);

                    string rawUrl = context.RawUrl ?? string.Empty;

                    if (rawUrl.Contains("/cancel", StringComparison.OrdinalIgnoreCase))
                    {
                        context.RespondHtml("<html><body style='background:#121212;color:#fff;font-family:sans-serif;text-align:center;padding-top:50px;'><h2>Nenhuma alteração foi salva.</h2><p>Você já pode fechar esta aba.</p></body></html>", "text/html; charset=utf-8");
                        await Task.Delay(500, cancellationToken);
                        return null;
                    }

                    if (context.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        string? error = await strategy.ProcessPostAuthStepAsync(current, context.Body ?? string.Empty, linkedCts.Token);
                        if (error != null)
                        {
                            string retryHtml = await strategy.RenderPostAuthStepHtmlAsync(current, error, linkedCts.Token);
                            context.RespondHtml(retryHtml, "text/html; charset=utf-8");
                            continue;
                        }

                        context.RespondHtml(BuildSuccessHtml(), "text/html; charset=utf-8");
                        await Task.Delay(500, cancellationToken);
                        return current;
                    }

                    string html = await strategy.RenderPostAuthStepHtmlAsync(current, null, linkedCts.Token);
                    context.RespondHtml(html, "text/html; charset=utf-8");
                }
            }
            finally
            {
                httpServer.Stop();
            }

            throw new OperationCanceledException("A configuração foi cancelada.");
        }

        private static string BuildSuccessHtml() => @"
            <html><head><title>Configurações Salvas!</title></head>
            <body style='font-family:sans-serif;text-align:center;padding-top:50px;background:#121212;color:#1db954;'>
            <h2>Configurações salvas com sucesso!</h2>
            <p>Você já pode fechar esta janela.</p>
            </body></html>";
    }
}