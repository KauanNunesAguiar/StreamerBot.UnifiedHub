using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using StreamerBot.UnifiedHub.Core.Services.Http;

namespace StreamerBot.UnifiedHub.Integrations.Overlay.Services
{
    /// <summary>
    /// Busca e mantém em cache os badges reais da Twitch (moderador, VIP, sub, etc.)
    /// via API pública "badges.twitch.tv" - não exige Client-Id/token. Global + por canal
    /// (o badge de assinante tem arte diferente para cada streamer).
    /// </summary>
    public static class TwitchBadgeCache
    {
        private const string GlobalUrl = "https://badges.twitch.tv/v1/badges/global/display";
        private const string ChannelUrlTemplate = "https://badges.twitch.tv/v1/badges/channel/{0}/display";

        // setId -> (versionId -> imageUrl)
        private static Dictionary<string, Dictionary<string, string>> _global = new();
        private static readonly ConcurrentDictionary<string, Dictionary<string, Dictionary<string, string>>> _channelCache = new();
        private static DateTime _globalLoadedAt = DateTime.MinValue;
        private static readonly SemaphoreSlim _lock = new(1, 1);

        public static async Task<List<string>> ResolveBadgeUrlsAsync(string? rawBadges, string? broadcasterId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rawBadges))
                return [];

            await EnsureGlobalLoadedAsync(cancellationToken);

            var channelBadges = string.IsNullOrWhiteSpace(broadcasterId)
                ? null
                : await GetChannelBadgesAsync(broadcasterId!, cancellationToken);

            var urls = new List<string>();

            foreach (var pair in rawBadges.Split(','))
            {
                var parts = pair.Split('/');
                if (parts.Length != 2) continue;
                string setId = parts[0], versionId = parts[1];

                if (channelBadges != null && channelBadges.TryGetValue(setId, out var channelVersions) && channelVersions.TryGetValue(versionId, out var channelUrl))
                    urls.Add(channelUrl);
                else if (_global.TryGetValue(setId, out var globalVersions) && globalVersions.TryGetValue(versionId, out var globalUrl))
                    urls.Add(globalUrl);
            }

            return urls;
        }

        private static async Task EnsureGlobalLoadedAsync(CancellationToken cancellationToken)
        {
            if (_global.Count > 0 && DateTime.UtcNow - _globalLoadedAt < TimeSpan.FromHours(24))
                return;

            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_global.Count > 0 && DateTime.UtcNow - _globalLoadedAt < TimeSpan.FromHours(24))
                    return;

                string json = await FetchJsonAsync(GlobalUrl, cancellationToken);
                _global = ParseBadgeSets(json);
                _globalLoadedAt = DateTime.UtcNow;
            }
            catch
            {
                // Mantém cache anterior (ou vazio) - overlay cai pros chips coloridos.
            }
            finally
            {
                _lock.Release();
            }
        }

        private static async Task<Dictionary<string, Dictionary<string, string>>?> GetChannelBadgesAsync(string broadcasterId, CancellationToken cancellationToken)
        {
            if (_channelCache.TryGetValue(broadcasterId, out var cached))
                return cached;

            try
            {
                string json = await FetchJsonAsync(string.Format(ChannelUrlTemplate, broadcasterId), cancellationToken);
                var parsed = ParseBadgeSets(json);
                _channelCache[broadcasterId] = parsed;
                return parsed;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<string> FetchJsonAsync(string url, CancellationToken cancellationToken)
        {
            var client = HubServiceProvider.GetHttpClient("Overlay");
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
#if NET48
            return await response.Content.ReadAsStringAsync();
#else
            return await response.Content.ReadAsStringAsync(cancellationToken);
#endif
        }

        private static Dictionary<string, Dictionary<string, string>> ParseBadgeSets(string json)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();
            var sets = JObject.Parse(json)["badge_sets"] as JObject;
            if (sets == null) return result;

            foreach (var set in sets.Properties())
            {
                var versions = new Dictionary<string, string>();
                if (set.Value["versions"] is JObject versionsObj)
                {
                    foreach (var version in versionsObj.Properties())
                    {
                        string? imageUrl = version.Value["image_url_2x"]?.ToString() ?? version.Value["image_url_1x"]?.ToString();
                        if (!string.IsNullOrEmpty(imageUrl))
                            versions[version.Name] = imageUrl!;
                    }
                }
                result[set.Name] = versions;
            }

            return result;
        }
    }
}