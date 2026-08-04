using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StreamerBot.UnifiedHub.Core;

namespace StreamerBot.UnifiedHub.TestApp
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var hub = new UnifiedHub.Core.UnifiedHub();
            await hub.InitializeAsync();

            ExibirMenu();

            while (true)
            {
                Console.Write("\nDigite o número da opção (ou 'm' para ver o menu, '0' para sair): ");
                string opcao = Console.ReadLine()?.Trim();

                if (opcao == "0") break;

                switch (opcao?.ToLower())
                {
                    case "m":
                        ExibirMenu();
                        break;

                    case "1":
                        string musica = await hub.ObterMusicaAtualAsync();
                        Console.WriteLine($"\n[Tocando Agora]: {musica}");
                        break;

                    case "2":
                        string link = await hub.ObterLinkMusicaAtualAsync();
                        Console.WriteLine($"\n[Link]: {link}");
                        break;

                    case "3":
                        string ultima = await hub.ObterUltimaMusicaTocadaAsync();
                        Console.WriteLine($"\n[Última Tocada]: {ultima}");
                        break;

                    case "4":
                        Console.Write("Quantidade de músicas da fila (padrão 5): ");
                        int.TryParse(Console.ReadLine(), out int qtd);
                        qtd = qtd <= 0 ? 5 : qtd;

                        List<string> fila = await hub.ObterFilaReproducaoAsync(qtd);
                        Console.WriteLine($"\n--- Próximas {fila.Count} Músicas na Fila ---");
                        for (int i = 0; i < fila.Count; i++)
                        {
                            Console.WriteLine($"{i + 1}. {fila[i]}");
                        }
                        break;

                    case "5":
                        await hub.AlternarPlayPauseAsync();
                        Console.WriteLine("\n[Player]: Alternado Play/Pause.");
                        break;

                    case "6":
                        await hub.RetomarPlayerAsync();
                        Console.WriteLine("\n[Player]: Reprodução retomada.");
                        break;

                    case "7":
                        await hub.PausarPlayerAsync();
                        Console.WriteLine("\n[Player]: Reprodução pausada.");
                        break;

                    case "8":
                        await hub.ProximaMusicaAsync();
                        Console.WriteLine("\n[Player]: Pulou para a próxima música.");
                        break;

                    case "9":
                        await hub.MusicaAnteriorAsync();
                        Console.WriteLine("\n[Player]: Voltou para a música anterior.");
                        break;

                    case "10":
                        await hub.ReiniciarMusicaAtualAsync();
                        Console.WriteLine("\n[Player]: Música reiniciada do início.");
                        break;

                    case "11":
                        Console.Write("Digite o Link ou URI da música para pedir: ");
                        string inputPedir = Console.ReadLine()?.Trim();
                        bool pediu = await hub.PedirMusicaAsync(inputPedir);
                        Console.WriteLine(pediu ? "\n[Sucesso]: Música adicionada à fila!" : "\n[Erro]: Falha ao pedir música.");
                        break;

                    case "12":
                        bool removeu = await hub.RemoverUltimoPedidoAsync();
                        Console.WriteLine(removeu ? "\n[Sucesso]: Pedido removido (música avançada)." : "\n[Erro]: Falha ao remover pedido.");
                        break;

                    case "13":
                        var playlists = await hub.ObterPlaylistsDoUsuarioAsync();
                        Console.WriteLine($"\n--- Playlists do Usuário ({playlists.Count}) ---");
                        foreach (var pl in playlists)
                        {
                            Console.WriteLine($"ID: {pl.Id} | Nome: {pl.Name}");
                        }
                        break;

                    case "14":
                        Console.Write("Digite o ID ou URI da Playlist: ");
                        string playlistId = Console.ReadLine()?.Trim();
                        await hub.TocarPlaylistAsync(playlistId);
                        Console.WriteLine("\n[Player]: Iniciando reprodução da playlist.");
                        break;

                    case "15":
                        Console.Write("Digite o ID da Playlist destino: ");
                        string targetPlaylist = Console.ReadLine()?.Trim();
                        Console.Write("Digite o Link/URI da música (deixe em branco para usar a tocando agora): ");
                        string trackUri = Console.ReadLine()?.Trim();

                        bool addPl = await hub.AdicionarMusicaAPlaylistAsync(targetPlaylist, string.IsNullOrEmpty(trackUri) ? null : trackUri);
                        Console.WriteLine(addPl ? "\n[Sucesso]: Música adicionada à playlist!" : "\n[Erro]: Falha ao adicionar à playlist.");
                        break;

                    case "16":
                        bool adicionado = await hub.AdicionarMusicaAtualAPlaylistAtualAsync();
                        Console.WriteLine(adicionado
                            ? "\n[Sucesso]: Música atual adicionada à playlist em reprodução!"
                            : "\n[Erro]: Não foi possível adicionar. Certifique-se de que está tocando uma música a partir de uma playlist de sua propriedade.");
                        break;

                    default:
                        Console.WriteLine("\nOpção inválida. Digite 'm' para ver o menu.");
                        break;
                }
            }
        }

        private static void ExibirMenu()
        {
            Console.WriteLine("\n=======================================================");
            Console.WriteLine("              STREAMERBOT UNIFIED HUB - SPOTIFY        ");
            Console.WriteLine("=======================================================");
            Console.WriteLine(" --- Informações ---");
            Console.WriteLine("  1. Display Current Spotify Song");
            Console.WriteLine("  2. Display Current Spotify Song Link");
            Console.WriteLine("  3. Display Last Played Song");
            Console.WriteLine("  4. Display Request Queue / Get Next X Songs");
            Console.WriteLine("\n --- Controle de Reprodução ---");
            Console.WriteLine("  5. Alternar Play/Pause");
            Console.WriteLine("  6. Resume Spotify Player");
            Console.WriteLine("  7. Pause Spotify Player");
            Console.WriteLine("  8. Skip Spotify Song");
            Console.WriteLine("  9. Play Previous Spotify Song");
            Console.WriteLine(" 10. Restart Current Spotify Song");
            Console.WriteLine("\n --- Fila & Pedidos ---");
            Console.WriteLine(" 11. Send Spotify Song Request");
            Console.WriteLine(" 12. Remove Last Song Request");
            Console.WriteLine("\n --- Playlists ---");
            Console.WriteLine(" 13. View Playlists");
            Console.WriteLine(" 14. Play Selected Playlist");
            Console.WriteLine(" 15. Add Song to Spotify Playlist");
            Console.WriteLine(" 16. Add Current Song to Currently Playing Playlist");
            Console.WriteLine("=======================================================");
            Console.WriteLine("  m. Mostrar Menu novamente");
            Console.WriteLine("  0. Sair");
            Console.WriteLine("=======================================================");
        }
    }
}