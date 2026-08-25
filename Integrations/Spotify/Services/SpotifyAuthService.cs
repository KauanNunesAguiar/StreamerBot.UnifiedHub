using Newtonsoft.Json;
using SpotifyAPI.Web;
using StreamerBot.UnifiedHub.Core.Abstractions;
using StreamerBot.UnifiedHub.Integrations.Spotify.Extensions;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;

namespace StreamerBot.UnifiedHub.Integrations.Spotify.Services
{
    public class SpotifyAuthService
    {
        private static readonly OAuthClient _oauthClient = new();

        public static async Task<SpotifyClient> EnsureAuthenticatedAsync(
            SpotifyConfig config,
            SpotifyOAuthHandler oauthHandler,
            IConfigManager? configManager = null,
            CancellationToken cancellationToken = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "A configuração do Spotify não pode ser nula.");

            if (!config.IsAuthenticated)
            {
                Log("Nenhuma configuração encontrada. Abrindo painel no navegador...");
                return await ReconfigureAsync(config, oauthHandler, configManager, cancellationToken);
            }

            try
            {
                Log("Credenciais encontradas. Conectando diretamente ao Spotify...");
                return await CreateClientAsync(config, configManager, cancellationToken);
            }
            catch (Exception ex)
            {
                Log($"Falha ao autenticar com as credenciais salvas ({ex.Message}). Redirecionando para o navegador...");
                return await ReconfigureAsync(config, oauthHandler, configManager, cancellationToken);
            }
        }

        public static async Task<SpotifyClient> ReconfigureAsync(
            SpotifyConfig config,
            SpotifyOAuthHandler oauthHandler,
            IConfigManager? configManager = null,
            CancellationToken cancellationToken = default)
        {
            Log("[DEBUG] Iniciando ReconfigureAsync...");

            var result = await oauthHandler.AuthenticateUserAsync(config, cancellationToken);

            if (string.IsNullOrEmpty(result.RefreshToken) || string.IsNullOrEmpty(result.ClientId) || string.IsNullOrEmpty(result.ClientSecret))
            {
                Log("[ERRO] Resultado do OAuth retornou credenciais em branco ou nulas.");
                throw new InvalidOperationException("Falha ao obter as novas credenciais do Spotify.");
            }

            var extra = new Dictionary<string, string>(result.ExtraSettings, StringComparer.OrdinalIgnoreCase);

            // 1. Atualizar credenciais de autenticação (Limpa tokens antigos da memória)
            config.ClientId = result.ClientId;
            config.ClientSecret = result.ClientSecret;
            config.RefreshToken = result.RefreshToken;
            config.AccessToken = string.Empty; // Reseta para forçar renovação limpa
            config.TokenExpiration = DateTime.MinValue;

            // 2. Mapeamento de Playlist
            if (extra.TryGetValue("playlistId", out string? playlistId) && !string.IsNullOrWhiteSpace(playlistId))
            {
                config.PlaylistId = playlistId;
            }

            // 3. Mapeamento de VoteSkip
            if (extra.TryGetValue("voteSkipThreshold", out string? rawThreshold) && int.TryParse(rawThreshold, out int threshold))
            {
                config.VoteSkipThreshold = threshold;
            }

            // 3.1 Mapeamento de QueueSize
            if (extra.TryGetValue("QueueSize", out string? rawQueueSize) && int.TryParse(rawQueueSize, out int queueSize) && queueSize > 0)
                config.QueueSize = queueSize;

            // 3.2 Mapeamento de BotName / BotImageUrl
            if (extra.TryGetValue("BotName", out string? botName) && !string.IsNullOrWhiteSpace(botName))
                config.BotName = botName;

            if (extra.TryGetValue("BotImageUrl", out string? botImageUrl))
                config.BotImageUrl = botImageUrl;

            // 3.3 Mapeamento de PollingIntervalMs
            if (extra.TryGetValue("PollingIntervalMs", out string? rawPolling) && int.TryParse(rawPolling, out int pollingMs) && pollingMs >= 1000)
                config.PollingIntervalMs = pollingMs;

            // 4. Mapeamento de Mensagens
            foreach (var setting in extra)
            {
                if (setting.Key.StartsWith("Msg:", StringComparison.OrdinalIgnoreCase))
                {
                    string msgKey = setting.Key["Msg:".Length..];
                    config.Messages[msgKey] = setting.Value;
                }
                else if (setting.Key.StartsWith("MsgEnabled:", StringComparison.OrdinalIgnoreCase))
                {
                    string msgKey = setting.Key["MsgEnabled:".Length..];
                    if (bool.TryParse(setting.Value, out bool enabled))
                        config.MessageEnabled[msgKey] = enabled;
                }
            }

            // 5. Gera o novo SpotifyClient com o RefreshToken RECENTE obtido da web
            var client = await CreateClientFromRefreshTokenAsync(
                config.ClientId,
                config.ClientSecret,
                config.RefreshToken,
                cancellationToken
            );

            // 6. PERSISTÊNCIA: Grava o estado final atualizado no arquivo
            if (configManager != null)
            {
                try
                {
                    var appConfig = configManager.Load();
                    appConfig.SetSpotifyConfig(config);
                    configManager.Save(appConfig);

                    Log("[SUCCESS] Configurações do Spotify salvas no disco com sucesso!");
                }
                catch (Exception ex)
                {
                    Log($"[ERRO CRÍTICO] Falha ao tentar gravar no arquivo de configuração: {ex.Message}");
                }
            }

            return client;
        }

        public static async Task<SpotifyClient> CreateClientAsync(
            SpotifyConfig config,
            IConfigManager? configManager = null,
            CancellationToken cancellationToken = default)
        {
            bool hasValidAccessToken = !string.IsNullOrEmpty(config.AccessToken)
                && config.TokenExpiration > DateTime.UtcNow.AddMinutes(1);

            if (hasValidAccessToken)
            {
                Log("Reutilizando Access Token salvo (ainda válido)...");
                var tokenResponse = new AuthorizationCodeTokenResponse
                {
                    AccessToken = config.AccessToken,
                    RefreshToken = config.RefreshToken,
                    ExpiresIn = (int)(config.TokenExpiration - DateTime.UtcNow).TotalSeconds,
                    CreatedAt = DateTime.UtcNow
                };

                var spotifyConfig = SpotifyClientConfig
                    .CreateDefault()
                    .WithAuthenticator(new AuthorizationCodeAuthenticator(config.ClientId, config.ClientSecret, tokenResponse));

                var client = new SpotifyClient(spotifyConfig);
                var me = await client.UserProfile.Current(cancellationToken);
                Log($"Conectado com sucesso como: {me.DisplayName} ({me.Id})");

                return client;
            }
            else
            {
                Log("Access Token ausente ou expirado. Solicitando novo ao Spotify...");

                // Cria um novo cliente já realizando a troca limpa pelo RefreshToken
                var client = await CreateClientFromRefreshTokenAsync(
                    config.ClientId,
                    config.ClientSecret,
                    config.RefreshToken,
                    cancellationToken
                );

                // Salva os novos tokens atualizados no disco se o manager for fornecido
                if (configManager != null)
                {
                    var appConfig = configManager.Load();
                    appConfig.SetSpotifyConfig(config);
                    configManager.Save(appConfig);
                }

                return client;
            }
        }

        public static async Task<SpotifyClient> CreateClientFromRefreshTokenAsync(
            string clientId,
            string clientSecret,
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            var refreshResponse = await _oauthClient.RequestToken(
                new AuthorizationCodeRefreshRequest(clientId, clientSecret, refreshToken),
                cancellationToken
            );

            var tokenResponse = new AuthorizationCodeTokenResponse
            {
                AccessToken = refreshResponse.AccessToken,
                RefreshToken = refreshToken,
                ExpiresIn = refreshResponse.ExpiresIn,
                CreatedAt = refreshResponse.CreatedAt,
                Scope = refreshResponse.Scope,
                TokenType = refreshResponse.TokenType
            };

            var spotifyConfig = SpotifyClientConfig
                .CreateDefault()
                .WithAuthenticator(new AuthorizationCodeAuthenticator(clientId, clientSecret, tokenResponse));

            var client = new SpotifyClient(spotifyConfig);

            // Valida a conexão testando a busca do perfil
            var me = await client.UserProfile.Current(cancellationToken);
            Log($"Conectado com sucesso como: {me.DisplayName} ({me.Id})");

            return client;
        }

        public static async Task<string> ExchangeCodeForRefreshTokenAsync(
            string clientId,
            string clientSecret,
            string code,
            string redirectUri,
            CancellationToken cancellationToken = default)
        {
            var response = await _oauthClient.RequestToken(
                new AuthorizationCodeTokenRequest(clientId, clientSecret, code, new Uri(redirectUri)),
                cancellationToken
            );

            return response.RefreshToken;
        }

        private static void Log(string message) => Console.WriteLine($"[SpotifyAuthService] {message}");
    }
}