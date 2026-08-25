using StreamerBot.UnifiedHub.Core.Abstractions;
using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Core.Services
{
    /// <summary>
    /// Wrapper fino reutilizável que monta o OAuthFlowHandler e delega a execução.
    /// Cada integração fornece sua própria IOAuthFlowStrategy (montada com o config
    /// específico dela) e mantém seu próprio wrapper de domínio por cima deste.
    /// </summary>
    public class OAuthHandler(ILocalHttpServer httpServer, IBrowserService browserService)
    {
        private readonly ILocalHttpServer _localHttpServer = httpServer;
        private readonly IBrowserService _browserService = browserService;

        public async Task<OAuthResult> AuthenticateUserAsync(
            IOAuthFlowStrategy strategy,
            string clientId,
            string clientSecret,
            string redirectUri,
            CancellationToken cancellationToken = default)
        {
            var flowHandler = new OAuthFlowHandler(_localHttpServer, _browserService, strategy);
            return await flowHandler.RunAsync(clientId, clientSecret, redirectUri, cancellationToken);
        }
    }
}