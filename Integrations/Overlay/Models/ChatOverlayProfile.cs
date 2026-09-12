using Newtonsoft.Json;

namespace StreamerBot.UnifiedHub.Integrations.Overlay.Models
{
    public class ChatOverlayProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Perfil";
        public bool Enabled { get; set; } = false;

        // ---------- Web ----------
        public int Port { get; set; } = 8081;
        public string Endpoint { get; set; } = "/ws";

        // ---------- Funcionamento ----------
        public int MaxMessages { get; set; } = 50;
        public int MessageDelayMs { get; set; } = 12000;
        public ChatOverlayVisibilityRules Visibility { get; set; } = new();

        // ---------- Aparência ----------
        public bool ShowTimestamp { get; set; } = false;
        public bool TopToBottom { get; set; } = false;

        public ChatOverlayTextAppearance Text { get; set; } = new();
        public ChatOverlayBoxAppearance Box { get; set; } = new();
        public ChatOverlayAnimationSettings Animation { get; set; } = new();
        public ChatOverlayBackgroundAppearance Background { get; set; } = new();

        // ---------- Legado (já existia) ----------
        public int EmoteSize { get; set; } = 28;
        public int BadgeSize { get; set; } = 18;
        public bool ShowBadges { get; set; } = true;

        // ---------- CSS ----------
        /// <summary>
        /// Conteúdo completo do textarea de CSS. O gerador (JS) só regenera o trecho
        /// entre os marcadores "/* AUTO-GENERATED START */" e "/* AUTO-GENERATED END */";
        /// tudo fora disso é preservado como escrito manualmente.
        /// </summary>
        public string CustomCss { get; set; } = string.Empty;

        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented);
    }
}