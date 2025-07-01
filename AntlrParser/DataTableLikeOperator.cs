namespace AntlrParser
{
    public static class DataTableLikeOperator
    {
        public static bool Like(string value, string pattern)
        {
            if (value == null || pattern == null) return false;

            // Convert DataTable LIKE pattern to Regex
            string regexPattern = pattern
                .Replace("*", ".*")
                .Replace("%", ".*")
                .Replace("?", ".");

            return System.Text.RegularExpressions.Regex.IsMatch(value, regexPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
    }
}