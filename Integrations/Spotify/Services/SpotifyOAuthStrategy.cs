using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Web;
using SpotifyAPI.Web;
using StreamerBot.UnifiedHub.Core.Abstractions;
using StreamerBot.UnifiedHub.Core.Models;
using StreamerBot.UnifiedHub.Core.Services;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyOAuthStrategy(SpotifyConfig? spotifyConfig = null, IConfigManager? configManager = null) : IOAuthFlowStrategy
    {
        private const string LoginHtmlResourceName = "Integrations.Spotify.Assets.spotify-login.html";
        private const string SettingsHtmlResourceName = "Integrations.Spotify.Assets.spotify-settings.html";
        private readonly SpotifyConfig? _spotifyConfig = spotifyConfig;
        private readonly IConfigManager? _configManager = configManager;

        public string InvalidCredentialsMessage =>
            "O Client ID informado é inválido ou não existe no Spotify Developer Dashboard.";

        public bool HasPostAuthStep => true;

        public string BuildAuthorizationUrl(string clientId, string redirectUri)
        {
            string scopes = Uri.EscapeDataString(
                "user-read-currently-playing " +
                "user-read-playback-state " +
                "user-modify-playback-state " +
                "user-read-recently-played " +
                "user-library-modify " +
                "user-library-read " +
                "playlist-read-private " +
                "playlist-read-collaborative " +
                "playlist-modify-public " +
                "playlist-modify-private"
            );

            return $"https://accounts.spotify.com/authorize?response_type=code&client_id={clientId}&scope={scopes}&redirect_uri={Uri.EscapeDataString(redirectUri)}";
        }

        public async Task<string> ExchangeCodeForRefreshTokenAsync(string clientId, string clientSecret, string code, string redirectUri)
            => await SpotifyAuthService.ExchangeCodeForRefreshTokenAsync(clientId, clientSecret, code, redirectUri);

        public string BuildExchangeErrorMessage(Exception ex) =>
            $"Falha ao validar credenciais (Client Secret pode estar incorreto): {ex.Message}";

        public Task<string> RenderFormHtml(string clientId, string clientSecret, string? erro)
        {
            var model = new OAuthLoginViewModel
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
                Error = erro
            };

            return Task.FromResult(SpotifyHtmlTemplates.RenderLogin(model));
        }

        public async Task<bool> ValidateCredentialsAsync(string clientId, string clientSecret, CancellationToken cancellationToken = default)
        {
            try
            {
                string authHeader = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token")
                {
                    Content = new FormUrlEncodedContent(
                    [
                        new KeyValuePair<string, string>("grant_type", "client_credentials")
                    ])
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);

                var response = await HubServiceProvider.GetHttpClient("Spotify").SendAsync(request, cancellationToken);
#if NET48
                string responseBody = await response.Content.ReadAsStringAsync();
#else
                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
#endif

                return response.IsSuccessStatusCode && !responseBody.Contains("invalid_client");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> RenderPostAuthStepHtmlAsync(OAuthResult result, string? error, CancellationToken cancellationToken)
        {
            var playlists = await FetchPlaylistsAsync(result, cancellationToken);

            var model = new SpotifySettingsViewModel
            {
                Error = error,
                Playlists = playlists,
                SelectedPlaylistId = _spotifyConfig?.PlaylistId ?? string.Empty,
                VoteSkipThreshold = _spotifyConfig?.VoteSkipThreshold ?? 3,
                QueueSize = _spotifyConfig?.QueueSize ?? 5,
                BotName = _spotifyConfig?.BotName ?? "Spotify",
                PollingIntervalMs = _spotifyConfig?.PollingIntervalMs ?? 5000,
                Messages = [.. SpotifyMessageCatalog.Definitions.Select(def =>
                {
                    bool isEnabled = true;
                    _spotifyConfig?.MessageEnabled.TryGetValue(def.Key, out isEnabled);
                    if (_spotifyConfig != null && !_spotifyConfig.MessageEnabled.ContainsKey(def.Key))
                        isEnabled = true;

                    return new MessageInputViewModel
                    {
                        Definition = new MessageDefinition
                        {
                            Key = def.Key,
                            Label = def.Label,
                            Description = def.Description,
                            Placeholders = def.Placeholders,
                            Enabled = isEnabled
                        },
                        Value = _spotifyConfig != null && _spotifyConfig.Messages.TryGetValue(def.Key, out var v) ? v : string.Empty
                    };
                })]
            };

            return SpotifyHtmlTemplates.RenderSettings(model);
        }

        public Task<string?> ProcessPostAuthStepAsync(OAuthResult result, string formBody, CancellationToken cancellationToken)
        {
            var formData = HttpUtility.ParseQueryString(formBody ?? string.Empty);

            string? playlistId = formData["playlistId"];
            if (string.IsNullOrWhiteSpace(playlistId))
                return Task.FromResult<string?>("Selecione uma playlist antes de salvar.");

            result.ExtraSettings["PlaylistId"] = playlistId;

            string? voteSkipThreshold = formData["voteSkipThreshold"];
            if (!string.IsNullOrWhiteSpace(voteSkipThreshold))
            {
                result.ExtraSettings["VoteSkipThreshold"] = voteSkipThreshold;
            }

            string? queueSize = formData["queueSize"];
            if (!string.IsNullOrWhiteSpace(queueSize))
                result.ExtraSettings["QueueSize"] = queueSize;

            string? botName = formData["botName"];
            if (!string.IsNullOrWhiteSpace(botName))
                result.ExtraSettings["BotName"] = botName;

            string? pollingIntervalMs = formData["pollingIntervalMs"];
            if (!string.IsNullOrWhiteSpace(pollingIntervalMs))
                result.ExtraSettings["PollingIntervalMs"] = pollingIntervalMs;

            foreach (var definition in SpotifyMessageCatalog.Definitions)
            {
                string fieldName = $"msg_{definition.Key}";
                string? value = formData[fieldName];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    result.ExtraSettings[$"Msg:{definition.Key}"] = value;
                }

                // Checkbox HTML só vem no formData quando marcado - ausência = desmarcado
                bool isChecked = !string.IsNullOrEmpty(formData[$"msgEnabled_{definition.Key}"]);
                result.ExtraSettings[$"MsgEnabled:{definition.Key}"] = isChecked.ToString();
            }

            return Task.FromResult<string?>(null);
        }

        private static async Task<List<SpotifyPlaylistInfo>> FetchPlaylistsAsync(OAuthResult result, CancellationToken cancellationToken)
        {
            var client = await SpotifyAuthService.CreateClientFromRefreshTokenAsync(
                result.ClientId, result.ClientSecret, result.RefreshToken);

            var playlists = new List<SpotifyPlaylistInfo>();
            var firstPage = await client.Playlists.CurrentUsers(new PlaylistCurrentUsersRequest { Limit = 50 }, cancellationToken);
            var allPlaylists = await client.PaginateAll(firstPage, cancellationToken: cancellationToken);

            foreach (var playlist in allPlaylists)
            {
                if (playlist?.Id == null) continue;

                playlists.Add(new SpotifyPlaylistInfo
                {
                    Id = playlist.Id,
                    Name = playlist.Name ?? "(sem nome)",
                    ImageUrl = playlist.Images?.FirstOrDefault()?.Url ?? string.Empty,
                    TracksTotal = playlist.Items?.Total ?? 0
                });
            }

            return playlists;
        }
    }
}