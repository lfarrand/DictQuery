using System.Text;
using System.Text.RegularExpressions;

namespace AntlrParser
{
    public static class DataTableLikeOperator
    {
        public static bool Like(string value, string pattern)
        {
            if (value == null || pattern == null)
            {
                return false;
            }

            // Check for illegal wildcard in the middle (e.g., a*e)
            var firstWildcard = pattern.IndexOfAny(new[] { '*', '%' });
            var lastWildcard = pattern.LastIndexOfAny(new[] { '*', '%' });
            if (firstWildcard > 0 && lastWildcard < pattern.Length - 1)
            {
                return false;
            }

            // Escape regex special chars except *, %, ?
            var sb = new StringBuilder();
            foreach (var c in pattern)
            {
                if (c == '*' || c == '%' || c == '?')
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append(Regex.Escape(c.ToString()));
                }
            }

            var regexPattern = sb.ToString()
                .Replace("*", ".*")
                .Replace("%", ".*")
                .Replace("?", ".");

            if (!pattern.StartsWith("*") && !pattern.StartsWith("%"))
            {
                regexPattern = "^" + regexPattern;
            }

            if (!pattern.EndsWith("*") && !pattern.EndsWith("%"))
            {
                regexPattern += "$";
            }

            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
            return regex.IsMatch(value);
        }
    }
}