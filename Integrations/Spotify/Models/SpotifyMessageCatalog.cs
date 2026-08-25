namespace StreamerBot.UnifiedHub.Integrations.Spotify.Models
{
    public class MessageDefinition
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Placeholders { get; set; } = [];
        public bool Enabled { get; set; } = true;
    }

    public static class SpotifyMessageCatalog
    {
        public static class Keys
        {
            public const string New = "new";
            public const string NewByRequest = "new_pedido";
            public const string Play = "play";
            public const string AlreadyPlaying = "ja_tocando";
            public const string Pause = "pause";
            public const string AlreadyPaused = "ja_pausado";
            public const string Prev = "prev";
            public const string Volume = "vol";
            public const string AddToQueue = "add";
            public const string AddNotFound = "add_nao_encontrado";
            public const string Undo = "undo";
            public const string UndoEmpty = "undo_vazio";
            public const string CurrentTrack = "atual";
            public const string NothingPlaying = "nada_tocando";
            public const string Playlist = "playlist";
            public const string Queue = "fila";
            public const string AddToPlaylist = "addplaylist";
            public const string ForceSkip = "skip";
            public const string VoteSkip = "voteskip";
            public const string JaVotou = "ja_votou";
            public const string SongHelp = "songhelp";
            public const string NoPermission = "sem_permissao";
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
                Placeholders = [ "{musica}", "{artista}", "{album}", "{link_musica}" ]
            },
            new MessageDefinition
            {
                Key = Keys.NewByRequest,
                Label = "Alerta de Nova Música (Pedida por Usuário)",
                Description = "Mensagem exibida quando a nova música tocando foi pedida por alguém no chat.",
                Placeholders = [ "{user}", "{musica}", "{artista}", "{album}", "{link_musica}" ]
            },
            new MessageDefinition
            {
                Key = Keys.Play,
                Label = "Retomar Reprodução",
                Description = "Mensagem exibida ao retomar a música.",
                Placeholders = [ "{user}" ]
            },
            new MessageDefinition
            {
                Key = Keys.AlreadyPlaying,
                Label = "Já Estava Tocando",
                Description = "Mensagem quando 'play' é chamado mas a música já estava tocando.",
                Placeholders = [ "{user}" ]
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
                Key = Keys.AlreadyPaused,
                Label = "Já Estava Pausado",
                Description = "Mensagem quando 'pause' é chamado mas já estava pausado.",
                Placeholders = [ "{user}" ]
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
                Key = Keys.AddNotFound,
                Label = "Música Não Encontrada",
                Description = "Mensagem quando a busca de 'add' não encontra nenhuma música correspondente.",
                Placeholders = [ "{user}" ]
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
                Key = Keys.UndoEmpty,
                Label = "Fila Vazia (Undo)",
                Description = "Mensagem quando o usuário tenta desfazer mas não tem música pendente na fila.",
                Placeholders = ["{user}"]
            },
            new MessageDefinition
            {
                Key = Keys.CurrentTrack,
                Label = "Informações da Música Atual",
                Description = "Mensagem ao consultar as informações da música atual.",
                Placeholders = [ "{musica}", "{artista}", "{album}", "{progresso}", "{link_musica}" ]
            },
            new MessageDefinition
            {
                Key = Keys.NothingPlaying,
                Label = "Nada Tocando",
                Description = "Mensagem ao consultar 'atual' sem nenhuma música tocando no momento.",
                Placeholders = []
            },
            new MessageDefinition
            {
                Key = Keys.Playlist,
                Label = "Informações da Playlist da Live",
                Description = "Mensagem ao consultar o link da playlist configurada para a live.",
                Placeholders = [ "{playlist_link}" ]
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
                Placeholders = [ "{user}", "{musica}", "{link_playlist}" ]
            },
            new MessageDefinition
            {
                Key = Keys.ForceSkip,
                Label = "Pular Forçado (Mod/Streamer)",
                Description = "Mensagem de pular música executado por moderadores/streamer.",
                Placeholders = [ "{user}", "{musica}", "{artista}", "{link_musica}" ],
            },
            new MessageDefinition
            {
                Key = Keys.Prev,
                Label = "Voltar para Música Anterior",
                Description = "Mensagem exibida ao voltar para a música anterior.",
                Placeholders = [ "{user}", "{musica}", "{artista}", "{link_musica}" ]
            },
            new MessageDefinition
            {
                Key = Keys.VoteSkip,
                Label = "Voto para Pular",
                Description = "Mensagem ao registrar um voto para pular a música.",
                Placeholders = [ "{user}", "{musica}", "{artista}", "{link_musica}", "{votos_atuais}", "{votos_necessarios}" ]
            },
            new MessageDefinition
            {
                Key = Keys.JaVotou,
                Label = "Usuário já votou",
                Description = "Mensagem resposta para usuários que tentaram votar para pular a música mais de uma vez.",
                Placeholders = [ "{user}" ]
            },
            new MessageDefinition
            {
                Key = Keys.NoPermission,
                Label = "Sem Permissão",
                Description = "Mensagem exibida quando alguém sem permissão tenta usar um comando. A checagem de quem pode usar cada comando é feita no Streamer.bot (permissões do trigger) - essa mensagem é só o texto de resposta.",
                Placeholders = [ "{user}" ]
            },
            new MessageDefinition
            {
                Key = Keys.SongHelp,
                Label = "Ajuda / Lista de Comandos",
                Description = "Mensagem de ajuda. Como os comandos (ex: !play, !pause) são configurados no Streamer.bot e não aqui, escreva livremente o texto listando os comandos que você criou.",
                Placeholders = [ "{user}", "{lista_comandos}" ],
            }
        ];
    }
}