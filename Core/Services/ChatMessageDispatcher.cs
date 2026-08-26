using System;
using System.Collections.Generic;
using System.Text;
using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Core.Services
{
    /// <summary>
    /// Centraliza a lógica de "montar e disparar mensagem de chat" comum a qualquer
    /// integração: checa se está habilitada, busca o template, formata placeholders
    /// e dispara o evento com o BotName correto.
    /// </summary>
    public class ChatMessageDispatcher(ChatIntegrationConfig config)
    {
        private readonly ChatIntegrationConfig _config = config ?? throw new ArgumentNullException(nameof(config));

        public event EventHandler<ChatMessageEventArgs>? OnChatMessage;

        public void Raise(string key, Dictionary<string, string>? placeholders = null)
        {
            if (_config.MessageEnabled.TryGetValue(key, out bool isEnabled) && !isEnabled)
                return;

            if (!_config.Messages.TryGetValue(key, out string? template) || string.IsNullOrWhiteSpace(template))
                return;

            string message = ChatMessageFormatter.Format(template, placeholders ?? []);
            string finalMessage = string.IsNullOrWhiteSpace(_config.BotName) ? message : $"[{_config.BotName}] {message}";
            StreamerBotContext.SendMessage(finalMessage);
            OnChatMessage?.Invoke(this, new ChatMessageEventArgs(_config.BotName, message));
        }
    }
}