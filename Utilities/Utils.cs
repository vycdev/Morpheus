using System.Text.RegularExpressions;

namespace Morpheus.Utilities;

public static class Utils
{
    public static readonly Version? AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

    // Precompiled regexes shared across calls
    private static readonly Regex _schemeRegex = new(@"\b(?:https?|ftp)://[\w\-\._~:/?#\[\]@!$&'()*+,;=%]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _mdLinkRegex = new(@"\[[^\]]+\]\((?:https?://|ftp://|www\.)[^)\s]+\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _bareDomainRegex = new(@"(?<=\s|^)(?:www\.)?(?:[a-z0-9](?:[a-z0-9\-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}(?::\d{1,5})?(?:/[^\s]*)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _ipRegex = new(@"(?<=\s|^)(?:(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)(?::\d{1,5})?(?:/[^\s]*)?(?=\s|$)", RegexOptions.Compiled);

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

        // Quick checks: schemes and markdown links are strong signals
        if (_schemeRegex.IsMatch(text)) return true;
        if (_mdLinkRegex.IsMatch(text)) return true;

        // Check for bare domain matches but validate matches individually so
        // that unrelated abbreviations (e.g. "e.g.") elsewhere in the text
        // don't cause a global false negative.
        var bareMatches = _bareDomainRegex.Matches(text);
        if (bareMatches.Count > 0)
        {
            foreach (Match m in bareMatches)
            {
                // Basic sanity: matched substring should contain a dot and a TLD-like suffix
                if (m.Success && m.Value.IndexOf('.') >= 0)
                {
                    // Avoid matching single-letter TLD-like fragments (should be enforced by regex)
                    // Return true for the first plausible domain-looking match.
                    return true;
                }
            }
        }

        // Check for IPv4-looking patterns
        if (_ipRegex.IsMatch(text)) return true;

        return false;
    }
}
