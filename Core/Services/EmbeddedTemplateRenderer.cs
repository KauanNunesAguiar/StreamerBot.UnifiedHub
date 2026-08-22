using System.Reflection;
using System.Text.RegularExpressions;

namespace StreamerBot.UnifiedHub.Core.Services
{
    public static class EmbeddedTemplateRenderer
    {
        private static readonly Regex TokenRegex = new(@"\{{1,2}([a-zA-Z0-9_]+)\}}{1,2}", RegexOptions.Compiled);

        public static string Load(Assembly assembly, string resourceName)
        {
            using Stream? stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Recurso embutido '{resourceName}' não encontrado.");

            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }

        public static string Render(string template, IDictionary<string, string> tags)
        {
            if (string.IsNullOrWhiteSpace(template)) return string.Empty;

            return TokenRegex.Replace(template, match =>
            {
                string key = match.Groups[1].Value;

                if (tags.TryGetValue(key, out var value))
                {
                    return value ?? string.Empty;
                }

                return match.Value; // Retorna a tag original se não encontrar no dicionário
            });
        }

        public static string LoadAndRender(Assembly assembly, string resourceName, IDictionary<string, string> tags)
        {
            string template = Load(assembly, resourceName);
            return Render(template, tags);
        }
    }
}