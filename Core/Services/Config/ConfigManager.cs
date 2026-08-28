using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using StreamerBot.UnifiedHub.Core.Abstractions;
using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Core.Services.Config
{
    public class ConfigManager : IConfigManager
    {
        private readonly string _configPath;
        private readonly string _defaultConfigPath;

        public ConfigManager(string configFileName = "config.json", string defaultConfigFileName = "defaultconfig.json")
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _configPath = Path.Combine(baseDir, configFileName);
            _defaultConfigPath = Path.Combine(baseDir, defaultConfigFileName);
        }

        public AppConfig Load()
        {
            // 1. Tenta carregar o arquivo de configuração principal (config.json)
            if (File.Exists(_configPath))
            {
                return ReadConfigFile(_configPath);
            }

            // 2. Se não existir, tenta carregar o arquivo padrão de fallback (defaultconfig.json)
            if (File.Exists(_defaultConfigPath))
            {
                var defaultConfig = ReadConfigFile(_defaultConfigPath);

                // Cria o config.json a partir do padrão para uso do usuário/app
                Save(defaultConfig);
                return defaultConfig;
            }

            // 3. Se nenhum existir, cria e salva uma nova instância vazia
            var fallbackConfig = new AppConfig();
            Save(fallbackConfig);
            return fallbackConfig;
        }

        public void Save(AppConfig config)
        {
            try
            {
                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConfigManager] Erro ao salvar {_configPath}: {ex.Message}");
            }
        }

        private static AppConfig ReadConfigFile(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConfigManager] Erro ao ler {path}: {ex.Message}");
                return new AppConfig();
            }
        }
    }
}