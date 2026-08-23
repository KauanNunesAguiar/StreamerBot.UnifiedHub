using System.Reflection;
using RazorLight;
using RazorLight.Razor;

namespace StreamerBot.UnifiedHub.Core.Services
{
    /// <summary>
    /// Renderiza templates Razor (.html/.css com sintaxe @) lidos diretamente dos recursos
    /// embutidos na DLL. O RazorLight cuida da leitura, compilação e cache - não é
    /// mais necessário carregar o Stream manualmente.
    /// </summary>
    public static class RazorTemplateRenderer
    {
        // Extension = string.Empty evita que o RazorLight force ".cshtml" no fim de toda
        // key - como já passamos o nome completo com extensão real (.html ou .css), ele
        // deve usar a key exatamente como veio, sem completar nada.
        private static readonly EmbeddedRazorProject _project = new(Assembly.GetExecutingAssembly(), "StreamerBot.UnifiedHub")
        {
            Extension = string.Empty
        };

        // rootNamespace fixo no namespace raiz do projeto. Cada chamador passa o caminho
        // completo do recurso (ex: "Integrations.Spotify.Assets.spotify-login.html") como
        // templateKey - assim uma única engine atende todas as integrações (Spotify, Twitch, YouTube...).
        private static readonly RazorLightEngine _engine = new RazorLightEngineBuilder()
            .UseProject(_project)
            .SetOperatingAssembly(Assembly.GetExecutingAssembly())
            .UseMemoryCachingProvider()
            .Build();

        public static async Task<string> RenderAsync<T>(string templateKey, T model)
            => await _engine.CompileRenderAsync(templateKey, model);
    }
}