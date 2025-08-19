using System.Text.RegularExpressions;

namespace Morpheus.Utilities;

public static class Utils
{
    public static readonly Version? AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

    public static string GetAssemblyVersion()
    {
        if (AssemblyVersion is null)
            throw new InvalidOperationException("Assembly version is null.");

        return $"{AssemblyVersion.Major}.{AssemblyVersion.Minor}.{AssemblyVersion.Build}.{AssemblyVersion.Revision}";
    }

    // Heuristic URL detection to reduce false positives/negatives.
    public static bool ContainsUrl(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        // scheme-based URLs (http, https, ftp)
        var schemeRegex = new Regex(@"\b(?:https?|ftp)://[\w\-\._~:/?#\[\]@!$&'()*+,;=%]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // markdown-style links: [text](url)
        var mdLinkRegex = new Regex(@"\[[^\]]+\]\((?:https?://|www\.)[^)\s]+\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // bare domains like example.com or sub.example.co.uk (with optional port/path)
        var bareDomainRegex = new Regex(@"\b(?:[a-z0-9](?:[a-z0-9\-]{0,61}[a-z0-9])?\.)+[a-z]{2,}\b(?:[:/][^\s]*)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // IP addresses (v4)
        var ipRegex = new Regex(@"\b(?:\d{1,3}\.){3}\d{1,3}(?::\d+)?(?:/\S*)?\b", RegexOptions.Compiled);

        if (schemeRegex.IsMatch(text)) return true;
        if (mdLinkRegex.IsMatch(text)) return true;

        // to avoid matching common words with dots (like 'e.g.'), ensure bare domain has a TLD-like suffix
        if (bareDomainRegex.IsMatch(text))
        {
            // filter out false positives: common abbreviations
            var lower = text.ToLowerInvariant();
            if (lower.Contains("e.g.") || lower.Contains("i.e.") || lower.Contains("mr.") || lower.Contains("mrs."))
                return false;
            return true;
        }

        if (ipRegex.IsMatch(text)) return true;

        return false;
    }
}
