
using Newtonsoft.Json;
using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Core.Extensions
{
    public static class AppConfigExtensions
    {
        public static T GetIntegrationConfig<T>(this AppConfig appConfig, string key) where T : class, new()
        {
            if (appConfig.IntegrationSettings.TryGetValue(key, out var value) && value != null)
            {
                if (value is T typed) return typed;
                if (value is JObject jObject) return jObject.ToObject<T>() ?? new T();
                return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value)) ?? new T();
            }

            var newConfig = new T();
            appConfig.IntegrationSettings[key] = newConfig;
            return newConfig;
        }

        public static void SetIntegrationConfig<T>(this AppConfig appConfig, string key, T config) where T : class
            => appConfig.IntegrationSettings[key] = config;
    }
}