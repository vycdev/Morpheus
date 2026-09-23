using System.Text.RegularExpressions;

namespace Morpheus.Utilities;

public static class Utils
{
    public static readonly Version? AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

    // Precompiled regexes shared across calls
    private static readonly Regex _schemeRegex = new(@"\b(?:https?|ftp)://[\w\-\._~:/?#\[\]@!$&'()*+,;=%]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _mdLinkRegex = new(@"\[[^\]]+\]\((?<url>(?:https?://|ftp://|www\.)[^)\s]+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    // Do not backtrack to a valid-looking prefix of a malformed port or hostname.
    private static readonly Regex _bareDomainRegex = new(@"(?<![a-z0-9.!#$%&'*+/=?^_`{|}~@-])(?:www\.)?(?:[a-z0-9](?:[a-z0-9\-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}(?::\d{1,5})?(?![\w:%+-]|\.[a-z0-9])(?![a-z0-9.!#$%&'*+/=?^_`{|}~-]*@)(?:/[^\s]*)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _ipRegex = new(@"(?<!@\[)(?<!@\[IPv4:)(?<![\w.@/+%-])(?:(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)(?::\d{1,5})?(?:/[^\s]*)?(?![\w@/:%+-]|\.[a-z0-9]|[^\s@]*@)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

        // Schemes and markdown links are strong signals, but still require a
        // syntactically valid URI so malformed ports are not accepted.
        foreach (Match match in _schemeRegex.Matches(text))
        {
            if (IsValidAbsoluteUrl(match.Value))
                return true;
        }

        foreach (Match match in _mdLinkRegex.Matches(text))
        {
            if (IsValidAbsoluteUrl(match.Groups["url"].Value))
                return true;
        }

        // Check for bare domain matches but validate matches individually so
        // that unrelated abbreviations (e.g. "e.g.") elsewhere in the text
        // don't cause a global false negative.
        var bareMatches = _bareDomainRegex.Matches(text);
        if (bareMatches.Count > 0)
        {
            foreach (Match m in bareMatches)
            {
                // Basic sanity: matched substring should contain a dot and a TLD-like suffix
                if (m.Success && m.Value.IndexOf('.') >= 0 && HasValidPort(m.Value))
                {
                    // Avoid matching single-letter TLD-like fragments (should be enforced by regex)
                    // Return true for the first plausible domain-looking match.
                    return true;
                }
            }
        }

        // Check for IPv4-looking patterns with usable port numbers.
        foreach (Match match in _ipRegex.Matches(text))
        {
            if (HasValidPort(match.Value))
                return true;
        }

        return false;
    }

    private static bool HasValidPort(string value) => IsValidAbsoluteUrl($"http://{value}");

    private static bool IsValidAbsoluteUrl(string value)
    {
        string candidate = value.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? $"http://{value}"
            : value;

        return Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) &&
               !string.IsNullOrEmpty(uri.Host) &&
               (uri.Scheme == Uri.UriSchemeHttp ||
                uri.Scheme == Uri.UriSchemeHttps ||
                uri.Scheme == Uri.UriSchemeFtp);
    }
}
