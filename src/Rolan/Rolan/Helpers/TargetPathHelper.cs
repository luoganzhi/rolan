using System.IO;
using System.Text.RegularExpressions;

namespace Rolan.Helpers;

internal static class TargetPathHelper
{
    private static readonly Regex UriSchemePattern = new("^[A-Za-z][A-Za-z0-9+.-]{1,}:", RegexOptions.Compiled);

    public static string? NormalizeInput(string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            return null;

        var value = targetPath.Trim().Trim('"');
        if (value.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            return $"https://{value}";

        return value;
    }

    public static string Resolve(string targetPath)
    {
        var normalized = NormalizeInput(targetPath) ?? string.Empty;
        if (IsUrl(normalized))
            return normalized;

        var expanded = Environment.ExpandEnvironmentVariables(normalized);
        if (Path.IsPathFullyQualified(expanded))
            return expanded;

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, expanded));
    }

    public static bool IsUrl(string targetPath)
    {
        var normalized = NormalizeInput(targetPath);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        return normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
               || UriSchemePattern.IsMatch(normalized);
    }
}
