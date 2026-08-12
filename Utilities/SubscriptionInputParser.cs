using System.Text.RegularExpressions;

namespace Morpheus.Utilities;

internal static partial class SubscriptionInputParser
{
    internal sealed record SourceList(IReadOnlyList<string> Sources, ulong? ChannelId);
    internal sealed record RssSource(string Url, string? DisplayName);
    private sealed record RssUrlKey(
        string Scheme,
        string Host,
        int Port,
        string UserInfo,
        string PathAndQuery);

    public static SourceList ParseSources(string input)
    {
        List<string> sources = [];
        ulong? channelId = null;

        foreach (string token in SplitTokens(input))
        {
            Match channel = ChannelMentionRegex().Match(token);
            if (channel.Success && ulong.TryParse(channel.Groups[1].Value, out ulong parsedChannelId))
            {
                channelId ??= parsedChannelId;
                continue;
            }

            sources.Add(token);
        }

        return new SourceList(Deduplicate(sources), channelId);
    }

    public static IReadOnlyList<RssSource> ParseRssSources(string input)
    {
        string[] lines = input.Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lines.Length == 1)
        {
            string[] tokens = SplitRssTokens(lines[0]).ToArray();
            string[] urls = tokens.Where(IsHttpUrl).ToArray();
            if (urls.Length > 1)
                return urls
                    .GroupBy(GetRssUrlKey)
                    .Select(group => new RssSource(RemoveRssFragment(group.First()), null))
                    .ToArray();
        }

        List<RssSource> sources = [];
        foreach (string line in lines)
        {
            int separatorIndex = line.IndexOf('|');
            if (separatorIndex < 0)
            {
                int commaIndex = line.IndexOf(',');
                int queryIndex = line.IndexOf('?');
                if (commaIndex > 0 &&
                    (queryIndex < 0 || commaIndex < queryIndex) &&
                    IsHttpUrl(line[..commaIndex].Trim()))
                    separatorIndex = commaIndex;
            }

            string sourcePart = separatorIndex >= 0 ? line[..separatorIndex].Trim() : line;
            string? separatedName = separatorIndex >= 0 ? NullIfWhiteSpace(line[(separatorIndex + 1)..]) : null;
            string[] parts = sourcePart.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || !IsHttpUrl(parts[0]))
                continue;

            string? displayName = separatedName ?? (parts.Length == 2 ? NullIfWhiteSpace(parts[1]) : null);
            sources.Add(new RssSource(parts[0], displayName));
        }

        return sources
            .GroupBy(source => GetRssUrlKey(source.Url))
            .Select(group =>
            {
                RssSource source = group.First();
                return source with { Url = RemoveRssFragment(source.Url) };
            })
            .ToArray();
    }

    private static RssUrlKey GetRssUrlKey(string value)
    {
        Uri uri = new(value, UriKind.Absolute);
        return new RssUrlKey(
            uri.Scheme.ToLowerInvariant(),
            uri.IdnHost.ToLowerInvariant(),
            uri.Port,
            uri.UserInfo,
            uri.PathAndQuery);
    }

    internal static string RemoveRssFragment(string value)
    {
        int fragmentIndex = value.IndexOf('#');
        return fragmentIndex >= 0 ? value[..fragmentIndex] : value;
    }

    private static IEnumerable<string> SplitTokens(string input) => input
        .Split([' ', '\t', '\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IEnumerable<string> SplitRssTokens(string input) => RssUrlSeparatorRegex()
        .Split(input)
        .Where(token => !string.IsNullOrWhiteSpace(token));

    private static IReadOnlyList<string> Deduplicate(IEnumerable<string> values)
    {
        List<string> unique = [];
        HashSet<string> seenCaseSensitive = new(StringComparer.Ordinal);
        HashSet<string> seenCaseInsensitive = new(StringComparer.OrdinalIgnoreCase);

        foreach (string value in values)
        {
            HashSet<string> seen = YoutubeChannelIdRegex().IsMatch(value)
                ? seenCaseSensitive
                : seenCaseInsensitive;

            if (seen.Add(value))
                unique.Add(value);
        }

        return unique;
    }

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^<#(\\d+)>$")]
    private static partial Regex ChannelMentionRegex();

    [GeneratedRegex("^UC[0-9A-Za-z_-]{22}$")]
    private static partial Regex YoutubeChannelIdRegex();

    [GeneratedRegex(@"(?:\s*[,;]\s*|\s+)(?=https?://)", RegexOptions.IgnoreCase)]
    private static partial Regex RssUrlSeparatorRegex();
}
