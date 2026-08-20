using System.Reflection;

namespace StreamerBot.UnifiedHub.Core.Services
{
    public static class EmbeddedTemplateRenderer
    {
        public static string Load(Assembly assembly, string resourceName)
        {
            using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException($"Recurso embutido '{resourceName}' não encontrado.");

                using (StreamReader reader = new(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        public static string Render(string template, IDictionary<string, string> tags)
        {
            string result = template;
            foreach (var tag in tags)
            {
                result = result.Replace(tag.Key, tag.Value ?? string.Empty);
            }
            return result;
        }
    }
}