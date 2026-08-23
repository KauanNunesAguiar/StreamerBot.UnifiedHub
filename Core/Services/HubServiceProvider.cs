using Microsoft.Extensions.DependencyInjection;

namespace StreamerBot.UnifiedHub.Core.Services
{
    /// <summary>
    /// Container mínimo, isolado do resto da aplicação. Não é injetado em cascata -
    /// serve apenas como fábrica central de HttpClients nomeados por integração.
    /// Construído uma única vez (lazy) e mantido vivo durante todo o ciclo de vida da DLL.
    /// </summary>
    public static class HubServiceProvider
    {
        private static readonly Lazy<ServiceProvider> _provider = new(BuildProvider);

        private static ServiceProvider BuildProvider()
        {
            var services = new ServiceCollection();

            // Registre um client nomeado por integração. Ao adicionar Twitch/Veadotube,
            // basta incluir mais uma chamada AddHttpClient aqui - nada mais muda.
            services.AddHttpClient("Spotify", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            services.AddHttpClient("YouTube", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            });

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Retorna o HttpClient gerenciado (pool de handlers, DNS refresh) para a integração informada.
        /// </summary>
        public static HttpClient GetHttpClient(string integrationName)
        {
            var factory = _provider.Value.GetRequiredService<IHttpClientFactory>();
            return factory.CreateClient(integrationName);
        }
    }
}