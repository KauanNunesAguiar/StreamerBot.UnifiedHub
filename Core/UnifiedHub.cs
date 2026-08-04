using System;
using System.Threading.Tasks;
using StreamerBot.UnifiedHub.Core.Models;
using StreamerBot.UnifiedHub.Core.Services;
using StreamerBot.UnifiedHub.Integrations;

namespace StreamerBot.UnifiedHub.Core
{
    public class UnifiedHub
    {
        private readonly ConfigManager _configManager;
        private readonly SpotifyConfig _config;

        public SpotifyService Spotify { get; private set; }
        public OAuthListener OAuth { get; private set; }

        public UnifiedHub()
        {
            _configManager = new ConfigManager();
            _config = _configManager.LoadConfig();

            Spotify = new SpotifyService(_config);
            OAuth = new OAuthListener();
        }

        public async Task InitializeAsync()
        {
            Console.WriteLine("[SpotifyHub] Inicializando serviço...");

            // 1. Tenta autenticar em segundo plano se já houver Refresh Token salvo
            bool autenticado = await Spotify.InitializeAsync();
            if (autenticado)
            {
                Console.WriteLine("[SpotifyHub] Autenticado com sucesso via Refresh Token!");
                return;
            }

            // 2. Abre a página e só retorna quando tudo for validado com sucesso na API
            var (clientId, clientSecret, refreshToken) = await OAuth.ExecutarFluxoAutenticacaoEValidarAsync(
                _config.RedirectUri,
                _config,
                Spotify
            );

            if (!string.IsNullOrEmpty(refreshToken))
            {
                // SÓ SALVA NO CONFIG.JSON APÓS A CONFIRMAÇÃO DE SUCESSO REAL
                _config.ClientId = clientId;
                _config.ClientSecret = clientSecret;
                _config.RefreshToken = refreshToken;
                _configManager.SaveConfig(_config);

                Spotify.UpdateConfig(_config);
                await Spotify.InitializeAsync();
                Console.WriteLine("[SpotifyHub] Spotify configurado e autenticado com sucesso!");
            }
            else
            {
                Console.WriteLine("[SpotifyHub] Falha na autenticação do Spotify.");
            }
        }

        public async Task AlternarPlayPause()
        {
            string atual = await Spotify.GetCurrentlyPlayingAsync();
            if (atual.Contains("Nenhuma música") || atual.Contains("não autenticado"))
            {
                await Spotify.PlayAsync();
            }
            else
            {
                await Spotify.PauseAsync();
            }
        }

        public async Task ProximaMusica()
        {
            await Spotify.SkipNextAsync();
        }

        public async Task<string> ObterMusicaAtual()
        {
            return await Spotify.GetCurrentlyPlayingAsync();
        }
    }
}