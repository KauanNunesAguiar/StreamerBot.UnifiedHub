using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StreamerBot.UnifiedHub.Core.Abstractions;

namespace StreamerBot.UnifiedHub.Core.Services
{
    public class LocalHttpListener : ILocalHttpServer
    {
        private HttpListener? _listener;

        public void Start(string redirectUri)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(redirectUri.EndsWith("/") ? redirectUri : redirectUri + "/");
            _listener.Start();
        }

        public void Stop()
        {
            if (_listener != null && _listener.IsListening)
            {
                _listener.Stop();
                _listener.Close();
                _listener = null;
            }
        }

        public async Task<HttpListenerContextWrapper> WaitForRequestAsync(CancellationToken cancellationToken = default)
        {
            if (_listener == null || !_listener.IsListening)
                throw new InvalidOperationException("O servidor HTTP local não está rodando.");

            using (cancellationToken.Register(() => _listener?.Stop()))
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    string body = string.Empty;

                    if (context.Request.HasEntityBody)
                    {
                        using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                        {
                            body = await reader.ReadToEndAsync();
                        }
                    }

                    return new HttpListenerContextWrapper
                    {
                        Method = context.Request.HttpMethod,
                        RawUrl = context.Request.RawUrl ?? string.Empty,
                        Body = body,
                        RespondHtml = (htmlResponse, contentType) =>
                        {
                            byte[] buffer = Encoding.UTF8.GetBytes(htmlResponse);
                            context.Response.ContentType = contentType;
                            context.Response.ContentLength64 = buffer.Length;
                            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                            context.Response.OutputStream.Close();
                        },
                        Redirect = url =>
                        {
                            context.Response.Redirect(url);
                            context.Response.OutputStream.Close();
                        }
                    };
                }
                catch (HttpListenerException ex)
                {
                    throw new TaskCanceledException("A aguardo da requisição HTTP foi cancelado ou o servidor foi encerrado.", ex);
                }
            }
        }
    }
}