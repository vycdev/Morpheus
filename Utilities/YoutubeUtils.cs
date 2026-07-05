using System.Text.Json;
using System.Text.RegularExpressions;

namespace Morpheus.Utilities;

/// <summary>
/// Helper utilities for working with YouTube resources: resolving user-supplied channel
/// references (URLs / handles / ids) to canonical channel ids, and fetching a channel's avatar.
/// </summary>
public static partial class YoutubeUtils
{
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0 Safari/537.36";

    [GeneratedRegex("^UC[0-9A-Za-z_-]{22}$")]
    private static partial Regex ChannelIdRegex();

    [GeneratedRegex("\"(?:channelId|externalId)\"\\s*:\\s*\"(UC[0-9A-Za-z_-]{22})\"")]
    private static partial Regex ChannelIdInHtmlRegex();

    [GeneratedRegex("/channel/(UC[0-9A-Za-z_-]{22})")]
    private static partial Regex ChannelIdInUrlRegex();

    /// <summary>
    /// Resolves a user-supplied reference to a canonical YouTube channel id.
    /// Accepts: a raw channel id ("UC..."), a /channel/UC... URL, or a handle / custom / user
    /// URL (e.g. "https://youtube.com/@Handle", "@Handle", "youtube.com/c/Name",
    /// "youtube.com/user/Name"), which are resolved by scraping the page.
    /// Returns null if it cannot be resolved.
    /// </summary>
    public static async Task<string?> ResolveChannelIdAsync(HttpClient httpClient, string input)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        if (string.IsNullOrWhiteSpace(input))
            return null;

        input = input.Trim();

        // Raw channel id
        if (ChannelIdRegex().IsMatch(input))
            return input;

        // /channel/UC... anywhere in the string
        Match urlMatch = ChannelIdInUrlRegex().Match(input);
        if (urlMatch.Success)
            return urlMatch.Groups[1].Value;

        // Build a URL to scrape for handles / custom names / bare "@handle"
        string url = BuildScrapeUrl(input);

        try
        {
            using HttpRequestMessage req = new(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd(BrowserUserAgent);
            using HttpResponseMessage resp = await httpClient.SendAsync(req).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;

            string html = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(html))
                return null;

            Match m = ChannelIdInHtmlRegex().Match(html);
            if (m.Success)
                return m.Groups[1].Value;

            // Fallback: canonical link tag often contains /channel/UC...
            Match canonical = ChannelIdInUrlRegex().Match(html);
            if (canonical.Success)
                return canonical.Groups[1].Value;

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string BuildScrapeUrl(string input)
    {
        // Already an http(s) URL — scrape it directly.
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return input;

        // Bare handle: "@name"
        if (input.StartsWith('@'))
            return $"https://www.youtube.com/{input}";

        // Something like "youtube.com/@name" without scheme
        if (input.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) ||
            input.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
            return $"https://{input.TrimStart('/')}";

        // Otherwise treat it as a handle
        return $"https://www.youtube.com/@{input}";
    }

    /// <summary>
    /// Uses the Innertube (youtubei) browse API to fetch a channel's avatar URL.
    /// Returns null on error or if thumbnails are not found.
    /// </summary>
    public static async Task<string?> GetChannelAvatarAsync(HttpClient httpClient, string channelId)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        if (string.IsNullOrWhiteSpace(channelId))
            return null;

        try
        {
            // 1) fetch youtube homepage to extract INNERTUBE_API_KEY and client version
            using HttpRequestMessage homeReq = new(HttpMethod.Get, "https://www.youtube.com");
            homeReq.Headers.UserAgent.ParseAdd(BrowserUserAgent);
            using HttpResponseMessage homeResp = await httpClient.SendAsync(homeReq).ConfigureAwait(false);
            if (!homeResp.IsSuccessStatusCode)
                return null;
            string homeHtml = await homeResp.Content.ReadAsStringAsync().ConfigureAwait(false);

            static string? ExtractValue(string hay, string marker)
            {
                int idx = hay.IndexOf(marker, StringComparison.Ordinal);
                if (idx < 0) return null;
                int start = idx + marker.Length;
                if (start >= hay.Length) return null;
                int end = hay.IndexOf('"', start);
                if (end < 0) return null;
                return hay[start..end];
            }

            string? apiKey = ExtractValue(homeHtml, "\"INNERTUBE_API_KEY\":\"");
            string? clientVersion = ExtractValue(homeHtml, "\"INNERTUBE_CLIENT_VERSION\":\"");
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(clientVersion))
                return null;

            // 2) call youtubei browse endpoint
            string url = $"https://www.youtube.com/youtubei/v1/browse?key={Uri.EscapeDataString(apiKey)}";
            var payload = new
            {
                context = new { client = new { clientName = "WEB", clientVersion } },
                browseId = channelId
            };

            string json = JsonSerializer.Serialize(payload);
            using HttpRequestMessage req = new(HttpMethod.Post, url)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            req.Headers.UserAgent.ParseAdd(BrowserUserAgent);

            using HttpResponseMessage resp = await httpClient.SendAsync(req).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;
            string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement root = doc.RootElement;

            static bool TryTraverse(JsonElement el, string[] path, out JsonElement result)
            {
                result = el;
                foreach (string p in path)
                {
                    if (result.ValueKind == JsonValueKind.Object && result.TryGetProperty(p, out JsonElement next))
                    {
                        result = next;
                        continue;
                    }
                    result = default;
                    return false;
                }
                return true;
            }

            static string? BestFromSources(JsonElement sources)
            {
                if (sources.ValueKind != JsonValueKind.Array)
                    return null;
                string? best = null;
                int bestW = 0;
                foreach (JsonElement it in sources.EnumerateArray())
                {
                    if (it.ValueKind != JsonValueKind.Object) continue;
                    if (it.TryGetProperty("url", out JsonElement urlEl) && urlEl.ValueKind == JsonValueKind.String)
                    {
                        string? s = urlEl.GetString();
                        int w = 0;
                        if (it.TryGetProperty("width", out JsonElement wEl) && wEl.TryGetInt32(out int wi)) w = wi;
                        if (w >= bestW && !string.IsNullOrWhiteSpace(s)) { bestW = w; best = s; }
                    }
                }
                return best;
            }

            // New Innertube layout
            string[] newPath = ["header", "pageHeaderRenderer", "content", "pageHeaderViewModel", "image", "decoratedAvatarViewModel", "avatar", "avatarViewModel", "image", "sources"];
            if (TryTraverse(root, newPath, out JsonElement sources))
            {
                string? best = BestFromSources(sources);
                if (!string.IsNullOrWhiteSpace(best)) return best;
            }

            // Older layouts
            string[] oldPath1 = ["header", "c4TabbedHeaderRenderer", "avatar", "thumbnails"];
            if (TryTraverse(root, oldPath1, out JsonElement thumbs))
            {
                string? best = BestFromSources(thumbs);
                if (!string.IsNullOrWhiteSpace(best)) return best;
            }

            string[] oldPath2 = ["header", "c4TabbedHeaderRenderer", "thumbnail", "thumbnails"];
            if (TryTraverse(root, oldPath2, out JsonElement thumbs2))
            {
                string? best = BestFromSources(thumbs2);
                if (!string.IsNullOrWhiteSpace(best)) return best;
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
