using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using StreamerBot.UnifiedHub.Core.Models;
using StreamerBot.UnifiedHub.Integrations;

namespace StreamerBot.UnifiedHub.Core.Services
{
    public class OAuthListener
    {
        public async Task<(string clientId, string clientSecret, string refreshToken)> ExecutarFluxoAutenticacaoEValidarAsync(
            string redirectUri,
            SpotifyConfig configAtual,
            SpotifyService spotifyService)
        {
            using (var listener = new HttpListener())
            {
                string prefix = redirectUri.EndsWith("/") ? redirectUri : redirectUri + "/";
                listener.Prefixes.Add(prefix);
                listener.Start();

                Console.WriteLine("[Setup] Requer autenticação. Abrindo página no navegador...");
                AbrirNavegador(prefix);

                string clientIdSalvo = configAtual?.ClientId ?? "";
                string clientSecretSalvo = configAtual?.ClientSecret ?? "";
                string erroMensagem = null;

                while (listener.IsListening)
                {
                    var context = await listener.GetContextAsync();
                    var request = context.Request;
                    var response = context.Response;

                    // 1. O usuário clica em "Entrar / Conectar" no formulário (POST)
                    if (request.HttpMethod == "POST")
                    {
                        using (var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding))
                        {
                            string body = await reader.ReadToEndAsync();
                            var formData = System.Web.HttpUtility.ParseQueryString(body);

                            clientIdSalvo = formData["clientId"]?.Trim();
                            clientSecretSalvo = formData["clientSecret"]?.Trim();

                            string scopes = Uri.EscapeDataString("user-read-currently-playing user-modify-playback-state user-read-playback-state");
                            string authUrl = $"https://accounts.spotify.com/authorize?response_type=code&client_id={clientIdSalvo}&scope={scopes}&redirect_uri={Uri.EscapeDataString(redirectUri)}";

                            response.Redirect(authUrl);
                            response.Close();
                        }
                    }
                    // 2. Erro vindo do próprio Spotify (ex: Client ID inválido ou autorização negada)
                    else if (request.QueryString["error"] != null)
                    {
                        string erroSpotify = request.QueryString["error"];
                        erroMensagem = erroSpotify.Contains("invalid_client")
                            ? "Client ID inválido ou não encontrado no Spotify Developer Dashboard."
                            : $"Erro do Spotify: {erroSpotify}";

                        RenderizarFormulario(response, clientIdSalvo, clientSecretSalvo, erroMensagem);
                    }
                    // 3. Recebeu o código temporário do Spotify — Tenta trocar pelo Refresh Token ANTES de responder
                    else if (request.QueryString["code"] != null)
                    {
                        string code = request.QueryString["code"];

                        try
                        {
                            // Valida as credenciais trocando o código pelo token definitivo
                            string refreshToken = await spotifyService.ExchangeCodeForRefreshTokenAsync(clientIdSalvo, clientSecretSalvo, code, redirectUri);

                            // SÓ EXIBE SUCESSO SE CHEGAR AQUI SEM ERROS
                            string successHtml = @"
                            <html>
                            <head><title>Conectado!</title></head>
                            <body style='font-family:sans-serif; text-align:center; padding-top:50px; background:#121212; color:#1db954;'>
                                <h2>Autenticação concluída com sucesso!</h2>
                                <p>Credenciais validadas e salvas. Pode fechar esta janela.</p>
                            </body>
                            </html>";

                            byte[] buffer = Encoding.UTF8.GetBytes(successHtml);
                            response.ContentType = "text/html; charset=utf-8";
                            response.ContentLength64 = buffer.Length;
                            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                            response.Close();

                            listener.Stop();
                            return (clientIdSalvo, clientSecretSalvo, refreshToken);
                        }
                        catch (APIException ex)
                        {
                            // Captura Client Secret incorreto ou divergente
                            erroMensagem = $"Falha na autenticação: {ex.Message}. Verifique se o Client Secret está correto.";
                            RenderizarFormulario(response, clientIdSalvo, clientSecretSalvo, erroMensagem);
                        }
                        catch (Exception ex)
                        {
                            erroMensagem = $"Erro ao validar token: {ex.Message}";
                            RenderizarFormulario(response, clientIdSalvo, clientSecretSalvo, erroMensagem);
                        }
                    }
                    // 4. Primeira exibição da página de formulário
                    else
                    {
                        RenderizarFormulario(response, clientIdSalvo, clientSecretSalvo, erroMensagem);
                    }
                }

                return (null, null, null);
            }
        }

        private void RenderizarFormulario(HttpListenerResponse response, string clientId, string clientSecret, string erro)
        {
            string divErro = string.IsNullOrEmpty(erro)
                ? ""
                : $"<div style='background:#e74c3c; color:#fff; padding:12px; border-radius:4px; margin-bottom:15px; font-size:13px; text-align:center; font-weight:bold;'>{erro}</div>";

            string formHtml = $@"
            <html>
            <head>
                <title>Configuração do Spotify</title>
                <style>
                    body {{ font-family: sans-serif; background-color: #121212; color: #fff; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; }}
                    .card {{ background: #181818; padding: 30px; border-radius: 8px; box-shadow: 0 4px 12px rgba(0,0,0,0.5); width: 360px; }}
                    label {{ font-size: 14px; color: #b3b3b3; }}
                    input {{ width: 100%; padding: 10px; margin: 8px 0 16px 0; border: 1px solid #333; border-radius: 4px; background: #282828; color: #fff; box-sizing: border-box; }}
                    button {{ width: 100%; padding: 12px; background: #1db954; border: none; border-radius: 4px; color: #fff; font-weight: bold; cursor: pointer; margin-top: 10px; }}
                    button:hover {{ background: #1ed760; }}
                </style>
            </head>
            <body>
                <div class='card'>
                    <h2 style='margin-top:0;'>Spotify Setup</h2>
                    {divErro}
                    <form method='POST'>
                        <label>Client ID:</label>
                        <input type='text' name='clientId' value='{clientId}' required />
                        <label>Client Secret:</label>
                        <input type='password' name='clientSecret' value='{clientSecret}' required />
                        <button type='submit'>Entrar / Conectar</button>
                    </form>
                </div>
            </body>
            </html>";

            byte[] buffer = Encoding.UTF8.GetBytes(formHtml);
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        private void AbrirNavegador(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OAuth] Não foi possível abrir o navegador: {ex.Message}");
            }
        }
    }
}