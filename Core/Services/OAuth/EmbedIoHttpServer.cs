using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;
using EmbedIO;
using EmbedIO.Actions;
using StreamerBot.UnifiedHub.Core.Abstractions;

namespace StreamerBot.UnifiedHub.Core.Services.OAuth
{
    /// <summary>
    /// Implementação de ILocalHttpServer usando EmbedIO. Faz a ponte entre o modelo
    /// "push" do EmbedIO (módulo recebe e deve responder) e o modelo "pull" que o
    /// OAuthFlowHandler espera (await WaitForRequestAsync), usando um Channel.
    /// </summary>
    public class EmbedIoHttpServer : ILocalHttpServer
    {
        private WebServer? _server;
        private readonly Channel<HttpListenerContextWrapper> _channel =
            Channel.CreateUnbounded<HttpListenerContextWrapper>();

        public void Start(string redirectUri)
        {
            var uri = new Uri(redirectUri);
            string listenUrl = $"{uri.Scheme}://{uri.Host}:{uri.Port}/";

            _server = new WebServer(o => o.WithUrlPrefix(listenUrl))
                .WithModule(new ActionModule("/", HttpVerbs.Any, HandleRequestAsync));

            _ = _server.RunAsync();
        }

        public void Stop()
        {
            _server?.Dispose();
            _server = null;
        }

        public async Task<HttpListenerContextWrapper> WaitForRequestAsync(CancellationToken cancellationToken = default)
            => await _channel.Reader.ReadAsync(cancellationToken);

        private async Task HandleRequestAsync(IHttpContext ctx)
        {
            // Mantém a requisição "presa" até o consumidor (OAuthFlowHandler) decidir a resposta.
            var responseReady = new System.Threading.Tasks.TaskCompletionSource<bool>();

            string body = string.Empty;
            if (ctx.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                body = await ctx.GetRequestBodyAsStringAsync();

            var wrapper = new HttpListenerContextWrapper
            {
                Method = ctx.Request.HttpMethod,
                RawUrl = ctx.Request.Url.PathAndQuery,
                Body = body,
                RespondHtml = (html, contentType) =>
                {
                    ctx.SendStringAsync(html, contentType, Encoding.UTF8).Wait();
                    responseReady.TrySetResult(true);
                },
                RespondStatusCode = statusCode =>
                {
                    ctx.Response.StatusCode = statusCode;
                    responseReady.TrySetResult(true);
                },
                Redirect = url =>
                {
                    ctx.Redirect(url);
                    responseReady.TrySetResult(true);
                }
            };

            await _channel.Writer.WriteAsync(wrapper);
            await responseReady.Task; // libera a thread do EmbedIO só quando o consumidor responder
        }
    }
}