using System.Collections.Generic;

namespace StreamerBot.UnifiedHub.Integrations.Overlay.Models
{
    /// <summary>
    /// Regras de "quem aparece no chat overlay". Membro = segue o canal mas não é
    /// inscrito (grátis). Não Membro = nem segue nem é inscrito.
    /// </summary>
    public class ChatOverlayVisibilityRules
    {
        public bool ShowMembers { get; set; } = true;
        public bool ShowNonMembers { get; set; } = true;
        public bool ShowSubscribers { get; set; } = true;
        public bool ShowVips { get; set; } = true;
        public bool ShowMods { get; set; } = true;
        public bool ShowBots { get; set; } = false;
        public bool ShowCommands { get; set; } = true;
        public List<string> CommandPrefixes { get; set; } = ["!"];
        public bool ShowYoutube { get; set; } = true;
        public bool ShowTwitch { get; set; } = true;
    }
}