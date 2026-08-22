using Newtonsoft.Json;

namespace StreamerBot.UnifiedHub.Core.Models
{
    public class AppConfig
    {
        // Seção genérica para configurações globais da aplicação/DLL
        public string Environment { get; set; } = "Development";
        public bool EnableLogging { get; set; } = true;

        // Dicionário genérico para armazenar configurações dinâmicas de diferentes serviços
        public Dictionary<string, object> IntegrationSettings { get; set; } = new();

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}