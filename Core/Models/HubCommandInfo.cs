namespace StreamerBot.UnifiedHub.Core.Models
{
    /// <summary>
    /// DTO leve e desacoplado do CommandData real do Streamer.bot. Evita que esta DLL precise
    /// referenciar a assembly do Streamer.bot.Plugin.Interface diretamente - quem faz a ponte
    /// é o próprio código C# inline do usuário, ao registrar o provider.
    /// </summary>
    public record HubCommandInfo(string Name, IReadOnlyList<string> Commands, bool Enabled);
}