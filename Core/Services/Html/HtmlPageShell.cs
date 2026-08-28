// Core/Services/Html/HtmlPageShell.cs
using System.Text;

namespace StreamerBot.UnifiedHub.Core.Services.Html
{
    public class PageShellOptions
    {
        public string Title { get; set; } = string.Empty;
        public string LogoIcon { get; set; } = "⚙️";
        public string HeaderTitle { get; set; } = string.Empty;
        public string HeaderSubtitle { get; set; } = string.Empty;
        public bool Wide { get; set; } = false;
        public string? Error { get; set; }
        public string? Success { get; set; }
        public string ExtraCss { get; set; } = string.Empty;
        public string ExtraScript { get; set; } = string.Empty;
    }

    /// <summary>
    /// Monta a estrutura comum de todas as páginas HTML da DLL (doctype, head, style,
    /// container, header, banners de erro/sucesso). Cada integração só precisa montar
    /// o conteúdo interno (form) e chamar Render passando esse HTML pronto.
    /// </summary>
    public static class HtmlPageShell
    {
        private static readonly string SharedCss = EmbeddedResourceReader.ReadText("Core.Assets.SharedStyles.css");
        private static readonly string ChatCss = EmbeddedResourceReader.ReadText("Core.Assets.ChatSettingsStyles.css");

        public static string Render(PageShellOptions options, string bodyHtml)
        {
            var sb = new StringBuilder();

            sb.Append("<!DOCTYPE html><html lang=\"pt-BR\"><head><meta charset=\"UTF-8\">");
            sb.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.Append("<title>").Append(HtmlComponents.E(options.Title)).Append("</title>");
            sb.Append("<link rel=\"icon\" href=\"data:,\">");
            sb.Append("<style>").Append(SharedCss).Append(ChatCss).Append(options.ExtraCss).Append("</style>");
            sb.Append("</head><body>");

            sb.Append("<div class=\"container").Append(options.Wide ? " wide" : "").Append("\">");

            sb.Append("<div class=\"header\">");
            sb.Append("<div class=\"logo-icon\">").Append(options.LogoIcon).Append("</div>");
            sb.Append("<h1>").Append(HtmlComponents.E(options.HeaderTitle)).Append("</h1>");
            if (!string.IsNullOrEmpty(options.HeaderSubtitle))
                sb.Append("<p>").Append(HtmlComponents.E(options.HeaderSubtitle)).Append("</p>");
            sb.Append("</div>");

            if (!string.IsNullOrEmpty(options.Error))
                sb.Append("<div class=\"error\">").Append(HtmlComponents.E(options.Error)).Append("</div>");
            else if (!string.IsNullOrEmpty(options.Success))
                sb.Append("<div class=\"success\">").Append(HtmlComponents.E(options.Success)).Append("</div>");

            sb.Append(bodyHtml);

            sb.Append("</div>"); // fecha .container

            if (!string.IsNullOrEmpty(options.ExtraScript))
                sb.Append("<script>").Append(options.ExtraScript).Append("</script>");

            sb.Append("</body></html>");
            return sb.ToString();
        }
    }
}