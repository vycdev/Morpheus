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
        string PathAndQuery,
        string Fragment);

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
            string[] tokens = SplitTokens(lines[0]).ToArray();
            string[] urls = tokens.Where(IsHttpUrl).ToArray();
            if (urls.Length > 1)
                return urls
                    .GroupBy(GetRssUrlKey)
                    .Select(group => new RssSource(group.First(), null))
                    .ToArray();
        }

        List<RssSource> sources = [];
        foreach (string line in lines)
        {
            int separatorIndex = line.IndexOf('|');
            if (separatorIndex < 0)
            {
                int commaIndex = line.IndexOf(',');
                if (commaIndex > 0 && IsHttpUrl(line[..commaIndex].Trim()))
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
            .Select(group => group.First())
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
            uri.PathAndQuery,
            uri.Fragment);
    }

    private static IEnumerable<string> SplitTokens(string input) => input
        .Split([' ', '\t', '\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyList<string> Deduplicate(IEnumerable<string> values) => values
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex("^<#(\\d+)>$")]
    private static partial Regex ChannelMentionRegex();
}
