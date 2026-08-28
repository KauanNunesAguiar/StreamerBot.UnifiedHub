using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace StreamerBot.UnifiedHub.Core.Services.Chat
{
    public static class ChatMessageFormatter
    {
        private static readonly Regex PlaceholderRegex = new(@"\{([a-zA-Z0-9_]+)\}", RegexOptions.Compiled);

        public static string Format(string template, IDictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(template))
                return string.Empty;

            var lookup = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);

            return PlaceholderRegex.Replace(template, match =>
                lookup.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
        }
    }
}