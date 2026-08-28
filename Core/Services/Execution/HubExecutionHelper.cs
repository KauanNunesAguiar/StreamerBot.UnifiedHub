using System;
using System.Collections.Generic;
using System.Text;
using StreamerBot.UnifiedHub.Core.Models;

namespace StreamerBot.UnifiedHub.Core.Services.Execution
{
    /// <summary>
    /// Encapsula o padrão repetido em todo Hub estático: checar se está pronto,
    /// executar a ação, converter exceções em HubResult amigável. Cada Hub concreto
    /// só informa sua checagem de "pronto" e a mensagem de "não inicializado".
    /// </summary>
    public class HubExecutionHelper(Func<bool> isReady, string notInitializedMessage)
    {
        private void EnsureReady()
        {
            if (!isReady())
                throw new InvalidOperationException(notInitializedMessage);
        }

        public async Task<HubResult> ExecuteAsync(Func<Task> action, string successMessage, string acao)
        {
            try { EnsureReady(); await action(); return HubResult.Ok(successMessage); }
            catch (Exception ex) { return HubResult.Fail(BuildFriendlyError(ex, acao)); }
        }

        public async Task<HubResult> ExecuteAsync<T>(Func<Task<T>> action, string successMessage, string acao)
        {
            try { EnsureReady(); var data = await action(); return HubResult.Ok(data!, successMessage); }
            catch (Exception ex) { return HubResult.Fail(BuildFriendlyError(ex, acao)); }
        }

        public async Task<HubResult> ExecuteAsync<T>(Func<Task<(T Data, string Message)>> action, string acao)
        {
            try { EnsureReady(); var (data, message) = await action(); return HubResult.Ok(data!, message); }
            catch (Exception ex) { return HubResult.Fail(BuildFriendlyError(ex, acao)); }
        }

        // Novo: para ações que já decidem Ok/Fail internamente (ex: RemoveLastAddedFromQueueAsync)
        public async Task<HubResult> ExecuteAsync(Func<Task<HubResult>> action, string acao)
        {
            try { EnsureReady(); return await action(); }
            catch (Exception ex) { return HubResult.Fail(BuildFriendlyError(ex, acao)); }
        }

        public HubResult Execute(Action action, string successMessage, string acao)
        {
            try { EnsureReady(); action(); return HubResult.Ok(successMessage); }
            catch (Exception ex) { return HubResult.Fail(BuildFriendlyError(ex, acao)); }
        }

        public HubResult Execute<T>(Func<T> action, string successMessage, string acao)
        {
            try { EnsureReady(); var data = action(); return HubResult.Ok(data!, successMessage); }
            catch (Exception ex) { return HubResult.Fail(BuildFriendlyError(ex, acao)); }
        }

        private static string BuildFriendlyError(Exception ex, string acao) => ex switch
        {
            InvalidOperationException => ex.Message,
            OperationCanceledException => $"A operação de {acao} foi cancelada.",
            _ => $"Erro ao {acao}: {ex}"
        };
    }
}