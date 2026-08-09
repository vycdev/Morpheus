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

    [GeneratedRegex("^[0-9A-Za-z_-]{11}$")]
    private static partial Regex VideoIdRegex();

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

        // Normalize supported references before inspecting or fetching them. This prevents
        // URL-shaped user input from selecting an arbitrary host or YouTube redirect path.
        Uri? scrapeUri = BuildScrapeUri(input);
        if (scrapeUri == null)
            return null;

        Match urlMatch = ChannelIdInUrlRegex().Match(scrapeUri.AbsolutePath);
        if (urlMatch.Success)
            return urlMatch.Groups[1].Value;

        try
        {
            using HttpRequestMessage req = new(HttpMethod.Get, scrapeUri);
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

    private static Uri? BuildScrapeUri(string input)
    {
        // Bare handle: "@name"
        if (input.StartsWith('@'))
            return BuildHandleUri(input[1..]);

        // Relative YouTube channel paths are unambiguous and safe to normalize.
        if (input.StartsWith('/') && !input.StartsWith("//", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate($"https://www.youtube.com{input}", UriKind.Absolute, out Uri? relativeUri))
                return null;

            return BuildCanonicalYoutubeUri(relativeUri);
        }

        if (Uri.TryCreate(input, UriKind.Absolute, out Uri? absoluteUri))
            return BuildCanonicalYoutubeUri(absoluteUri);

        // Something like "youtube.com/@name" without a scheme. Other slash-containing
        // values are rejected by the host check instead of being treated as handles.
        if (input.Contains('/'))
        {
            if (!Uri.TryCreate($"https://{input.TrimStart('/')}", UriKind.Absolute, out Uri? schemelessUri))
                return null;

            return BuildCanonicalYoutubeUri(schemelessUri);
        }

        // Otherwise treat it as a handle
        return BuildHandleUri(input);
    }

    private static Uri? BuildCanonicalYoutubeUri(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;

        if (uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            string videoId = uri.AbsolutePath.Trim('/');
            return VideoIdRegex().IsMatch(videoId)
                ? new Uri($"https://www.youtube.com/watch?v={videoId}")
                : null;
        }

        if (!IsYoutubeHost(uri.Host) || !IsSupportedYoutubePath(uri.AbsolutePath))
            return null;

        // Canonicalize the host and discard user-controlled query/fragment values. The
        // resolver only needs the channel path to scrape the corresponding YouTube page.
        return new Uri($"https://www.youtube.com{uri.AbsolutePath}");
    }

    private static Uri? BuildHandleUri(string handle) =>
        string.IsNullOrWhiteSpace(handle) || handle.Contains('/')
            ? null
            : new Uri($"https://www.youtube.com/@{Uri.EscapeDataString(handle)}");

    private static bool IsYoutubeHost(string host) =>
        host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportedYoutubePath(string path) =>
        path.StartsWith("/@", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/c/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/user/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/channel/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Uses the Innertube (youtubei) browse API to fetch a channel's avatar URL.
    /// Returns null on error or if thumbnails are not found.
    /// </summary>
    public static async Task<string?> GetChannelAvatarAsync(HttpClient httpClient, string channelId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        if (string.IsNullOrWhiteSpace(channelId))
            return null;

        try
        {
            // 1) fetch youtube homepage to extract INNERTUBE_API_KEY and client version
            using HttpRequestMessage homeReq = new(HttpMethod.Get, "https://www.youtube.com");
            homeReq.Headers.UserAgent.ParseAdd(BrowserUserAgent);
            using HttpResponseMessage homeResp = await httpClient.SendAsync(homeReq, cancellationToken).ConfigureAwait(false);
            if (!homeResp.IsSuccessStatusCode)
                return null;
            string homeHtml = await homeResp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

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

            using HttpResponseMessage resp = await httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;
            string body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
