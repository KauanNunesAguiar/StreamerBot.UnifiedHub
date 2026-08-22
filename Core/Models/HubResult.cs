using Newtonsoft.Json;

namespace StreamerBot.UnifiedHub.Core.Models
{
    /// <summary>
    /// Resultado padronizado para TODAS as ações da Hub (Spotify, Twitch, YouTube, etc).
    /// Data vem sempre como JSON serializado, permitindo um único contrato de retorno
    /// independente do tipo real do dado - facilita o consumo no Streamer.bot.
    /// </summary>
    public readonly record struct HubResult(bool Success, string Message, string? Data = null)
    {
        public static HubResult Ok(string message) => new(true, message);

        public static HubResult Ok(object data, string message) => new(true, message, JsonConvert.SerializeObject(data));

        public static HubResult Fail(string message) => new(false, message);
    }
}