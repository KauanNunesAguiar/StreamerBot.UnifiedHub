using System;
using System.IO;
using System.Reflection;

namespace StreamerBot.UnifiedHub.Core.Services
{
    public static class EmbeddedResourceReader
    {
        public static string ReadText(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string fullName = $"StreamerBot.UnifiedHub.{resourceName}";
            using var stream = assembly.GetManifestResourceStream(fullName)
                ?? throw new InvalidOperationException($"Recurso embutido não encontrado: {fullName}");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}