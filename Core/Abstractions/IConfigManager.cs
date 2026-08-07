using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Core.Abstractions
{
    public interface IConfigManager
    {
        AppConfig Load();
        void Save(AppConfig config);
    }
}