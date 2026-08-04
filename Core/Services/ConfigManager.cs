using System;
using System.IO;
using Newtonsoft.Json;
using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Core.Services
{
    public class ConfigManager
    {
        private readonly string _filePath;

        public ConfigManager(string filePath = "config.json")
        {
            _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);
            Console.WriteLine($"[ConfigManager] Salvo em {_filePath}");
        }

        public SpotifyConfig LoadConfig()
        {
            if (!File.Exists(_filePath))
            {
                var defaultConfig = new SpotifyConfig();
                SaveConfig(defaultConfig);
                return defaultConfig;
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                return JsonConvert.DeserializeObject<SpotifyConfig>(json) ?? new SpotifyConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConfigManager] Erro ao ler {_filePath}: {ex.Message}");
                return new SpotifyConfig();
            }
        }

        public void SaveConfig(SpotifyConfig config)
        {
            try
            {
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConfigManager] Erro ao salvar {_filePath}: {ex.Message}");
            }
        }
    }
}