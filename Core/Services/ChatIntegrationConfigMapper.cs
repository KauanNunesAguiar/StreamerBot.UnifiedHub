using System;
using System.Collections.Generic;
using System.Text;
using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Core.Services
{
    /// <summary>
    /// Aplica os campos comuns de ChatIntegrationConfig (BotLabel,
    /// PollingIntervalMs, Messages, MessageEnabled) a partir do dicionário de
    /// ExtraSettings retornado pelo fluxo de OAuth. Reutilizável por qualquer
    /// integração que tenha uma etapa pós-auth de configuração de chat.
    /// </summary>
    public static class ChatIntegrationConfigMapper
    {
        public static void ApplyExtraSettings(ChatIntegrationConfig config, IReadOnlyDictionary<string, string> extra)
        {
            if (extra.TryGetValue("BotLabel", out string? BotLabel) && !string.IsNullOrWhiteSpace(BotLabel))
                config.BotLabel = BotLabel;

            if (extra.TryGetValue("PollingIntervalMs", out string? rawPolling) && int.TryParse(rawPolling, out int pollingMs) && pollingMs >= 1000)
                config.PollingIntervalMs = pollingMs;

            foreach (var setting in extra)
            {
                if (setting.Key.StartsWith("Msg:", StringComparison.OrdinalIgnoreCase))
                {
                    string msgKey = setting.Key.Substring("Msg:".Length);
                    config.Messages[msgKey] = setting.Value;
                }
                else if (setting.Key.StartsWith("MsgEnabled:", StringComparison.OrdinalIgnoreCase))
                {
                    string msgKey = setting.Key.Substring("MsgEnabled:".Length);
                    if (bool.TryParse(setting.Value, out bool enabled))
                        config.MessageEnabled[msgKey] = enabled;
                }
            }
        }
    }
}