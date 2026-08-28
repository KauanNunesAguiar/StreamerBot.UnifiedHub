// Core/Services/Html/HtmlComponents.cs
using System.Net;
using System.Text;

namespace StreamerBot.UnifiedHub.Core.Services.Html
{
    /// <summary>
    /// Helpers para os pedaços internos repetidos entre telas de configuração
    /// (título de seção, campo de formulário, toggle, botão, link de cancelar).
    /// Não define layout de página - só fragmentos de HTML.
    /// </summary>
    public static class HtmlComponents
    {
        public static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        public static string SectionTitle(string text)
            => $"<div class=\"section-title\">{E(text)}</div>";

        public static string FormGroup(string labelFor, string labelText, string inputHtml, string? hint = null)
        {
            var sb = new StringBuilder();
            sb.Append("<div class=\"form-group\"><label for=\"").Append(E(labelFor)).Append("\">").Append(E(labelText)).Append("</label>");
            sb.Append(inputHtml);
            if (!string.IsNullOrEmpty(hint))
                sb.Append("<span class=\"field-hint\">").Append(E(hint)).Append("</span>");
            sb.Append("</div>");
            return sb.ToString();
        }

        public static string ToggleSwitch(string name, bool isChecked, string onChangeJs = "", string enabledLabel = "Habilitado", string disabledLabel = "Desabilitado")
        {
            var sb = new StringBuilder();
            sb.Append("<label class=\"toggle-switch\" onclick=\"event.stopPropagation()\">");
            sb.Append("<input type=\"checkbox\" name=\"").Append(E(name)).Append("\"").Append(isChecked ? " checked" : "");
            if (!string.IsNullOrEmpty(onChangeJs))
                sb.Append(" onchange=\"").Append(onChangeJs).Append("\"");
            sb.Append(">");
            sb.Append("<span class=\"toggle-track\"></span>");
            sb.Append("<span class=\"toggle-label\">").Append(isChecked ? enabledLabel : disabledLabel).Append("</span>");
            sb.Append("</label>");
            return sb.ToString();
        }

        public static string SubmitButton(string text)
            => $"<button type=\"submit\" class=\"btn-submit\">{E(text)}</button>";

        public static string CancelLink(string href, string text)
            => $"<div style=\"text-align: center; margin-top: 15px;\">" +
               $"<a href=\"{E(href)}\" style=\"color: var(--text-sub); text-decoration: none; font-size: 13px;\">{E(text)}</a></div>";

        /// <summary>Envolve dois blocos de HTML no grid de duas colunas (settings-grid / col / col-sticky).</summary>
        public static string TwoColumnGrid(string leftColumnHtml, string rightColumnHtml, bool stickyRight = false)
        {
            var sb = new StringBuilder();
            sb.Append("<div class=\"settings-grid\">");
            sb.Append("<div class=\"col\">").Append(leftColumnHtml).Append("</div>");
            sb.Append("<div class=\"col").Append(stickyRight ? " col-sticky" : "").Append("\">").Append(rightColumnHtml).Append("</div>");
            sb.Append("</div>");
            return sb.ToString();
        }
    }
}