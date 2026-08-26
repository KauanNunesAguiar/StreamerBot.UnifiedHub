using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using SpotifyAPI.Web;
using StreamerBot.UnifiedHub.Core.Abstractions;
using StreamerBot.UnifiedHub.Core.Services;
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

            ApplyExtraSettingsToConfig(config, extra);

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

        public static void ApplyExtraSettingsToConfig(SpotifyConfig config, IReadOnlyDictionary<string, string> extra)
        {
            if (extra.TryGetValue("playlistId", out string? playlistId) && !string.IsNullOrWhiteSpace(playlistId))
                config.PlaylistId = playlistId;
            else if (extra.TryGetValue("PlaylistId", out string? playlistId2) && !string.IsNullOrWhiteSpace(playlistId2))
                config.PlaylistId = playlistId2;

            if (extra.TryGetValue("voteSkipThreshold", out string? rawThreshold) && int.TryParse(rawThreshold, out int threshold))
                config.VoteSkipThreshold = threshold;
            else if (extra.TryGetValue("VoteSkipThreshold", out string? rawThreshold2) && int.TryParse(rawThreshold2, out int threshold2))
                config.VoteSkipThreshold = threshold2;

            if (extra.TryGetValue("QueueSize", out string? rawQueueSize) && int.TryParse(rawQueueSize, out int queueSize) && queueSize > 0)
                config.QueueSize = queueSize;

            ChatIntegrationConfigMapper.ApplyExtraSettings(config, extra);
        }

        private static void Log(string message) => Console.WriteLine($"[SpotifyAuthService] {message}");
    }
}