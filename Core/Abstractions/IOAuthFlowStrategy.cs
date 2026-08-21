namespace StreamerBot.UnifiedHub.Core.Abstractions
{
    public interface IOAuthFlowStrategy
    {
        string InvalidCredentialsMessage { get; }

        string BuildAuthorizationUrl(string clientId, string redirectUri);
        Task<bool> ValidateCredentialsAsync(string clientId, string clientSecret, CancellationToken cancellationToken = default);
        Task<string> ExchangeCodeForRefreshTokenAsync(string clientId, string clientSecret, string code, string redirectUri);
        string RenderFormHtml(string clientId, string clientSecret, string? error);
        string BuildExchangeErrorMessage(Exception ex);
    }
}