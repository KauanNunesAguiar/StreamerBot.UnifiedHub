// Esse é um Program.cs que estou usando de teste no projeto "StreamerBot.UnifiedHub.TestApp",
// ele é copiado automáticamente após cada complilação

using Newtonsoft.Json;
using StreamerBot.UnifiedHub.Core.Models;
using StreamerBot.UnifiedHub.Integrations.Spotify.Hubs;
using StreamerBot.UnifiedHub.Integrations.Spotify.Models;

namespace StreamerBot.UnifiedHub.TestApp
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string userId = "00000000";
            string userName = "console";
            bool isModOrStreamer = false;

            Console.WriteLine("=============================================");
            Console.WriteLine(" TESTANDO STREAMERBOT UNIFIED HUB (HUB API) ");
            Console.WriteLine("=============================================\n");

            using var cts = new CancellationTokenSource();

            SpotifyHub.OnChatMessage += (sender, args) =>
            {
                Console.WriteLine($"[CHAT] {args.Message}");
            };

            Task inputTask = Task.CompletedTask;

            try
            {
                // Inicialização da integração via Hub
                var initResult = await SpotifyHub.InitializeAsync(pollingIntervalMs: 1000, cancellationToken: cts.Token);

                if (!initResult.Success)
                {
                    Console.WriteLine($"❌ Falha ao inicializar o Hub: {initResult.Message}");
                    return;
                }

                Console.WriteLine($" Conectado ao Spotify via Hub!");
                Console.WriteLine(" Comandos disponíveis:");
                Console.WriteLine(" - 'play' | 'pause' | 'prev' | 'vol x' | 'add x' | 'undo' | 'atual' | 'playlist' | 'fila' | 'addplaylist' | 'skip' | 'voteskip' | 'songHelp' | 'config' | 'sair'\n");

                inputTask = Task.Run(async () =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        var input = Console.ReadLine()?.Trim();
                        if (string.IsNullOrWhiteSpace(input)) continue;

                        var command = input.Split(' ')[0].ToLower();

                        try
                        {
                            switch (command)
                            {
                                case "play":
                                    PrintHubResult(await SpotifyHub.ResumeAsync(userName, cts.Token));
                                    break;

                                case "pause":
                                    PrintHubResult(await SpotifyHub.PauseAsync(userName, cts.Token));
                                    break;

                                case "prev":
                                    PrintHubResult(await SpotifyHub.PreviousAsync(userName, cts.Token));
                                    break;

                                case "vol":
                                    string volArg = input.Replace("vol ", "", StringComparison.OrdinalIgnoreCase).Trim();
                                    if (int.TryParse(volArg, out int vol))
                                    {
                                        PrintHubResult(await SpotifyHub.SetVolumeAsync(vol, userName, cts.Token));
                                    }
                                    else
                                    {
                                        Console.WriteLine("⚠️ Informe um número válido para o volume. Ex: vol 50");
                                    }
                                    break;

                                case "add":
                                    if (input.Length <= 4)
                                    {
                                        Console.WriteLine("⚠️ Informe o URI ou URL da música. Ex: add <url_ou_uri>");
                                        break;
                                    }
                                    string trackInput = input[3..].Trim();
                                    PrintHubResult(await SpotifyHub.AddToQueueAsync(trackInput, userId, userName, cts.Token));
                                    break;

                                case "undo":
                                case "removelast":
                                    PrintHubResult(await SpotifyHub.RemoveLastAddedFromQueueAsync(userId, isModOrStreamer, cts.Token));
                                    break;

                                case "playlist":
                                    PrintHubResult(await SpotifyHub.ShowPlaylistInfoAsync(cts.Token));
                                    break;

                                case "atual":
                                    var currentResult = await SpotifyHub.GetCurrentTrackAsync(cts.Token);
                                    if (currentResult.Success && !string.IsNullOrEmpty(currentResult.Data))
                                    {
                                        var track = JsonConvert.DeserializeObject<SpotifyTrackInfo>(currentResult.Data);
                                        if (track != null)
                                        {
                                            string progressText = $"{track.Player?.ProgressMs / 1000}s / {track.Player?.DurationMs / 1000}s";
                                            Console.WriteLine($"🎵 Tocando agora: {track.Media?.Title} - {track.Media?.Artist} [{progressText}]");
                                            Console.WriteLine($"🔗 {track.Identifiers?.Url}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"⚠️ {currentResult.Message}");
                                    }
                                    break;

                                case "fila":
                                    var queueResult = await SpotifyHub.GetQueueAsync(cancellationToken: cts.Token);
                                    if (queueResult.Success && !string.IsNullOrEmpty(queueResult.Data))
                                    {
                                        var queue = JsonConvert.DeserializeObject<List<SpotifyTrackInfo>>(queueResult.Data);
                                        if (queue == null || queue.Count == 0)
                                        {
                                            Console.WriteLine("📋 Fila vazia.");
                                        }
                                        else
                                        {
                                            Console.WriteLine("📋 Próximas músicas na fila:");
                                            for (int i = 0; i < queue.Count; i++)
                                            {
                                                Console.WriteLine($"  {i + 1}. {queue[i].Media?.Title} - {queue[i].Media?.Artist}");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"⚠️ {queueResult.Message}");
                                    }
                                    break;

                                case "addplaylist":
                                    PrintHubResult(await SpotifyHub.AddCurrentTrackToPlaylistAsync(userName, cts.Token));
                                    break;

                                case "skip":
                                    PrintHubResult(await SpotifyHub.ForceSkipAsync(userName, cts.Token));
                                    break;

                                case "voteskip":
                                    var voteResult = await SpotifyHub.VoteSkipAsync(userId, cts.Token);
                                    if (voteResult.Success && !string.IsNullOrEmpty(voteResult.Data))
                                    {
                                        var voteData = JsonConvert.DeserializeObject<VoteSkipResult>(voteResult.Data);
                                        if (voteData != null)
                                        {
                                            Console.WriteLine($"✅ Voto registrado por @{userName}! ({voteData.CurrentVotes}/{voteData.RequiredVotes})");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"⚠️ {voteResult.Message}");
                                    }
                                    break;

                                case "songHelp":
                                    PrintHubResult(await SpotifyHub.ShowSongHelpAsync(userName, cts.Token));
                                    break;

                                case "config":
                                    Console.WriteLine("\n[Spotify] Abrindo painel de configurações no navegador...");
                                    PrintHubResult(await SpotifyHub.ReconfigureAsync(cts.Token));
                                    break;

                                case "sair":
                                    Console.WriteLine("\nEncerrando aplicação...");
                                    cts.Cancel();
                                    break;

                                default:
                                    Console.WriteLine($"⚠️ Comando '{command}' não reconhecido.");
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"❌ Erro ao executar comando '{input}': {ex.Message}");
                        }
                    }
                }, cts.Token);

                await inputTask;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nA operação foi cancelada.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Erro no Spotify: {ex.Message}");
            }
            finally
            {
                cts.Cancel();
                SpotifyHub.Shutdown();

                if (!inputTask.IsCompleted)
                {
                    await inputTask;
                }
            }
        }

        private static void PrintHubResult(HubResult result)
        {
            if (result.Success)
            {
                Console.WriteLine($"✅ {result.Message}");
            }
            else
            {
                Console.WriteLine($"⚠️ {result.Message}");
            }
        }
    }
}