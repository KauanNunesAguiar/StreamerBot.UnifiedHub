using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Core.Abstractions
{
    public interface IOAuthFlowStrategy
    {
        string InvalidCredentialsMessage { get; }

        string BuildAuthorizationUrl(string clientId, string redirectUri);
        Task<bool> ValidateCredentialsAsync(string clientId, string clientSecret, CancellationToken cancellationToken = default);
        Task<string> ExchangeCodeForRefreshTokenAsync(string clientId, string clientSecret, string code, string redirectUri);
        Task<string> RenderFormHtml(string clientId, string clientSecret, string? error);
        string BuildExchangeErrorMessage(Exception ex);

        bool HasPostAuthStep => false;
        Task<string> RenderPostAuthStepHtmlAsync(OAuthResult result, string? error, CancellationToken cancellationToken)
            => Task.FromResult(string.Empty);
        Task<string?> ProcessPostAuthStepAsync(OAuthResult result, string formBody, CancellationToken cancellationToken)
            => Task.FromResult<string?>(null);
    }
}