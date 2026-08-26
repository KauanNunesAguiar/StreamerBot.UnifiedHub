using System;
using System.Collections.Generic;
using System.Text;

namespace StreamerBot.UnifiedHub.Core.Compatibility
{
#if NET48
    public static class NetStandardPolyfills
    {
        public static bool Contains(this string source, string value, StringComparison comparison)
            => source.IndexOf(value, comparison) >= 0;

        public static string Replace(this string source, string oldValue, string newValue, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(oldValue)) return source;
            int index = source.IndexOf(oldValue, comparison);
            if (index < 0) return source;

            var result = new StringBuilder();
            int previousIndex = 0;
            while (index != -1)
            {
                result.Append(source.Substring(previousIndex, index - previousIndex));
                result.Append(newValue);
                index += oldValue.Length;
                previousIndex = index;
                index = source.IndexOf(oldValue, index, comparison);
            }
            result.Append(source.Substring(previousIndex));
            return result.ToString();
        }
    }
#endif
}