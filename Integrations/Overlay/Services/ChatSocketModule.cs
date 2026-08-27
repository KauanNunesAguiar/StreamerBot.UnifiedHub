using System;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.WebSockets;
using Newtonsoft.Json;

namespace StreamerBot.UnifiedHub.Integrations.Overlay.Services
{
    /// <summary>
    /// Canal WebSocket "somente broadcast": não processa nada vindo do cliente, só
    /// envia o estado atual (modo) assim que o Browser Source conecta/recarrega, e
    /// distribui mensagens de chat/mudanças de modo para todos conectados.
    /// </summary>
    internal class ChatSocketModule : WebSocketModule
    {
        private readonly Func<object> _buildCurrentState;

        public ChatSocketModule(string urlPath, Func<object> buildCurrentState) : base(urlPath, true)
        {
            _buildCurrentState = buildCurrentState;
        }

        protected override Task OnMessageReceivedAsync(IWebSocketContext context, byte[] rxBuffer, IWebSocketReceiveResult rxResult)
            => Task.CompletedTask;

        protected override Task OnClientConnectedAsync(IWebSocketContext context)
            => SendAsync(context, JsonConvert.SerializeObject(_buildCurrentState()));

        public Task BroadcastJsonAsync(object payload)
            => BroadcastAsync(JsonConvert.SerializeObject(payload));
    }
}