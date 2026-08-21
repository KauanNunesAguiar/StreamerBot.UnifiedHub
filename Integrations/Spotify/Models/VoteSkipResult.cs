namespace StreamerBot.UnifiedHub.Integrations.Spotify.Models
{
    /// <summary>
    /// Resultado de uma tentativa de voto para pular a música atual.
    /// </summary>
    /// <param name="Accepted">Se o voto foi computado (false = voto rejeitado, ex: já votou ou nada tocando).</param>
    /// <param name="Message">Mensagem pronta para exibir no chat.</param>
    /// <param name="CurrentVotes">Quantidade de votos únicos acumulados para a música atual.</param>
    /// <param name="RequiredVotes">Quantidade de votos necessária para pular.</param>
    /// <param name="Skipped">Se esse voto foi o que atingiu o threshold e disparou o skip.</param>
    public record VoteSkipResult(bool Accepted, string Message, int CurrentVotes, int RequiredVotes, bool Skipped);
}