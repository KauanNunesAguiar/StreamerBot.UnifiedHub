using System.Collections.Generic;

namespace StreamerBot.UnifiedHub.Core.Models
{
    public class AppConfig
    {
        // Seção genérica para configurações globais da aplicação/DLL
        public string Environment { get; set; } = "Development";
        public bool EnableLogging { get; set; } = true;

        // Dicionário genérico para armazenar configurações dinâmicas de diferentes serviços
        // Exemplo de chave: "Spotify", "Twitch", "YouTube"
        public Dictionary<string, object> IntegrationSettings { get; set; } = new Dictionary<string, object>();
    }
}