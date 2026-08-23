namespace StreamerBot.UnifiedHub.Integrations.Spotify.Models
{
    public class MessageDefinition
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Placeholders { get; set; } = [];
    }

    public static class SpotifyMessageCatalog
    {
        public static class Keys
        {
            public const string New = "new";
            public const string Play = "play";
            public const string Pause = "pause";
            public const string Next = "next";
            public const string Prev = "prev";
            public const string Volume = "vol";
            public const string AddToQueue = "add";
            public const string Undo = "undo";
            public const string CurrentTrack = "atual";
            public const string Queue = "fila";
            public const string AddToPlaylist = "addplaylist";
            public const string ForceSkip = "skip";
            public const string VoteSkip = "voteskip";
        }

        /// <summary>
        /// Definições e metadados de cada mensagem para montagem de UI WEB e documentação de placeholders.
        /// NENHUM texto padrão fica fixado aqui - o padrão é provido via defaultconfig.json.
        /// </summary>
        public static List<MessageDefinition> Definitions { get; } =
        [
            new MessageDefinition
            {
                Key = Keys.New,
                Label = "Alerta de Nova Música",
                Description = "Mensagem exibida para avisar música atual.",
                Placeholders = ["{user}", "{musica}", "{artista}", "{album}", "{link_musica}" ]
            },
            new MessageDefinition
            {
                Key = Keys.Play,
                Label = "Retomar Reprodução",
                Description = "Mensagem exibida ao retomar a música.",
                Placeholders = ["{user}"]
            },
            new MessageDefinition
            {
                Key = Keys.Pause,
                Label = "Pausar Reprodução",
                Description = "Mensagem exibida ao pausar a música.",
                Placeholders = [ "{user}" ]
            },
            new MessageDefinition
            {
                Key = Keys.Next,
                Label = "Próxima Música",
                Description = "Mensagem exibida ao pular para a próxima faixa.",
                Placeholders = [ "{user}", "{musica}", "{artista}", "{link_musica}" ]
            },
            new MessageDefinition
            {
                Key = Keys.Prev,
                Label = "Música Anterior",
                Description = "Mensagem exibida ao voltar para a música anterior.",
                Placeholders = [ "{user}", "{musica}", "{artista}" ]
            },
            new MessageDefinition
            {
                Key = Keys.Volume,
                Label = "Ajuste de Volume",
                Description = "Mensagem exibida ao alterar o volume.",
                Placeholders = [ "{user}", "{volume}" ]
            },
            new MessageDefinition
            {
                Key = Keys.AddToQueue,
                Label = "Adicionar à Fila",
                Description = "Mensagem ao adicionar uma música à fila.",
                Placeholders = [ "{user}", "{musica}", "{artista}", "{posicao}", "{link_musica}" ]
            },
            new MessageDefinition
            {
                Key = Keys.Undo,
                Label = "Remover Última Música",
                Description = "Mensagem ao remover a última música enviada à fila.",
                Placeholders = [ "{user}", "{musica}" ]
            },
            new MessageDefinition
            {
                Key = Keys.CurrentTrack,
                Label = "Música Atual",
                Description = "Mensagem ao consultar as informações da música atual.",
                Placeholders = [ "{musica}", "{artista}", "{album}", "{progresso}", "{link_musica}" ]
            },
            new MessageDefinition
            {
                Key = Keys.Queue,
                Label = "Exibir Fila",
                Description = "Mensagem ao listar as próximas músicas da fila.",
                Placeholders = [ "{lista_fila}" ]
            },
            new MessageDefinition
            {
                Key = Keys.AddToPlaylist,
                Label = "Adicionar à Playlist",
                Description = "Mensagem ao salvar a música atual na playlist da live.",
                Placeholders = [ "{user}", "{musica}" ]
            },
            new MessageDefinition
            {
                Key = Keys.ForceSkip,
                Label = "Pular Forçado (Mod/Streamer)",
                Description = "Mensagem de pular música executado por moderadores/streamer.",
                Placeholders = [ "{user}" ]
            },
            new MessageDefinition
            {
                Key = Keys.VoteSkip,
                Label = "Voto para Pular",
                Description = "Mensagem ao registrar um voto para pular a música.",
                Placeholders = [ "{user}", "{votos_atuais}", "{votos_necessarios}" ]
            }
        ];
    }
}