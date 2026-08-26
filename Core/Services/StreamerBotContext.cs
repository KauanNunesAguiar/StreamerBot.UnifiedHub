using System;
using System.Collections.Generic;
using System.Text;
using StreamerBot.UnifiedHub.Core.Abstractions;

namespace StreamerBot.UnifiedHub.Core.Services
{
    /// <summary>
    /// Ponto único e estático para registrar a ponte com o Streamer.bot (IStreamerBotBridge).
    /// Qualquer integração consulta este contexto para enviar mensagens ou ler/gravar
    /// variáveis globais, sem precisar receber a dependência via construtor. Todas as
    /// chamadas são non-throwing: se nenhuma ponte foi registrada ainda, não fazem nada.
    /// </summary>
    public static class StreamerBotContext
    {
        private static IStreamerBotBridge? _bridge;

        public static bool IsConnected => _bridge != null;

        /// <summary>Registra a implementação real da ponte (chamado uma vez, no início do C# inline do Streamer.bot).</summary>
        public static void Connect(IStreamerBotBridge bridge) => _bridge = bridge;

        public static void SendMessage(string message, bool bot = true)
            => _bridge?.SendMessage(message, bot);

        public static string? GetGlobalVar(string name, bool persisted = true)
            => _bridge?.GetGlobalVar(name, persisted);

        public static void SetGlobalVar(string name, object value, bool persisted = true)
            => _bridge?.SetGlobalVar(name, value, persisted);
    }
}