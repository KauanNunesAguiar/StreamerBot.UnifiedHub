using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Core.Services
{
    /// <summary>
    /// Monta o texto de ajuda (lista de comandos disponíveis) casando os comandos
    /// registrados no Streamer.bot com o catálogo de mensagens de uma integração.
    /// </summary>
    public static class CommandHelpTextBuilder
    {
        public static string Build(IEnumerable<MessageDefinition> definitions, IEnumerable<HubCommandInfo> commands)
        {
            var linhas = new List<string>();

            foreach (var def in definitions)
            {
                var match = commands.FirstOrDefault(c =>
                    c.Enabled &&
                    c.Commands.Count > 0 &&
                    string.Equals(c.Name, def.Key, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                    continue;

                string triggers = string.Join("/", match.Commands);
                linhas.Add($"{triggers} - {def.Label}");
            }

            return linhas.Count > 0 ? string.Join(" | ", linhas) : "Nenhum comando encontrado.";
        }
    }
}