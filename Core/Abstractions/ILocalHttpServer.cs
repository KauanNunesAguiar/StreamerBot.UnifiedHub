namespace StreamerBot.UnifiedHub.Core.Abstractions
{
    public interface ILocalHttpServer
    {
        void Start(string redirectUri);
        void Stop();
        Task<HttpListenerContextWrapper> WaitForRequestAsync(CancellationToken cancellationToken = default);
    }

    // Encapsula o contexto da requisição HTTP de forma agnóstica para facilitar testes
    public class HttpListenerContextWrapper
    {
        public string Method { get; set; } = string.Empty;
        public string RawUrl { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public Action<string, string> RespondHtml { get; set; } = (html, contentType) => { };
        public Action<int> RespondStatusCode { get; set; } = statusCode => { };
        public Action<string> Redirect { get; set; } = url => { };
    }
}