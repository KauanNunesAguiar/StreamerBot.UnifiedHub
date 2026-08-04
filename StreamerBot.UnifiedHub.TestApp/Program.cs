using System;
using System.Threading.Tasks;

namespace StreamerBot.UnifiedHub.TestApp
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("      INICIANDO TESTES DO SPOTIFY HUB         ");
            Console.WriteLine("==============================================");

            var hub = new StreamerBot.UnifiedHub.Core.UnifiedHub();
            await hub.InitializeAsync();

            bool executando = true;
            while (executando)
            {
                Console.WriteLine("\n--- MENU SPOTIFY ---");
                Console.WriteLine("1. Obter musica atual");
                Console.WriteLine("2. Play / Pause");
                Console.WriteLine("3. Proxima musica");
                Console.WriteLine("0. Sair");
                Console.Write("Escolha uma opcao: ");

                string opcao = Console.ReadLine();

                switch (opcao)
                {
                    case "1":
                        string musica = await hub.ObterMusicaAtual();
                        Console.WriteLine($"\n🎶 Tocando agora: {musica}");
                        break;

                    case "2":
                        await hub.AlternarPlayPause();
                        Console.WriteLine("\nComando enviado!");
                        break;

                    case "3":
                        await hub.ProximaMusica();
                        Console.WriteLine("\nComando de pular enviado!");
                        break;

                    case "0":
                        executando = false;
                        break;

                    default:
                        Console.WriteLine("Opcao invalida!");
                        break;
                }
            }
        }
    }
}