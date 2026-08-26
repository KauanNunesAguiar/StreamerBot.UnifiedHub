using System;
using System.Collections.Generic;
using System.Text;

namespace StreamerBot.UnifiedHub.Core.Abstractions
{
    /// <summary>
    /// Abstrai a comunicação com o Streamer.bot (objeto CPH), mantendo esta DLL livre de
    /// qualquer referência à assembly do Streamer.bot.Plugin.Interface. Quem implementa
    /// essa interface é o código C# inline do usuário no Streamer.bot, repassando as
    /// chamadas reais para o CPH.
    /// </summary>
    public interface IStreamerBotBridge
    {
        void SendMessage(string message, bool bot = true);
        string? GetGlobalVar(string name, bool persisted = true);
        void SetGlobalVar(string name, object value, bool persisted = true);
    }
}