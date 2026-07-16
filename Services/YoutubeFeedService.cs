using System.Text.Json;
using System.Xml.Linq;
using Discord;

namespace Morpheus.Services;

/// <summary>
/// Fetches a YouTube channel's public uploads. The legacy Atom feed is preferred, with the
/// public uploads-playlist page used as a fallback when YouTube returns 404 for RSS.
/// </summary>
public class YoutubeFeedService(LogsService logsService)
{
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0 Safari/537.36";

    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Yt = "http://www.youtube.com/xml/schemas/2015";

    public record VideoEntry(string VideoId, string Title, string Link, DateTime Published);

    public async Task<(string? ChannelTitle, IReadOnlyList<VideoEntry> Entries)> FetchFeedAsync(string channelId, CancellationToken ct = default)
    {
        string feedUrl = $"https://www.youtube.com/feeds/videos.xml?channel_id={Uri.EscapeDataString(channelId)}";
        try
        {
            using HttpResponseMessage response = await HttpClient.GetAsync(feedUrl, ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                string xml = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return ParseAtomFeed(xml);
            }

            logsService.Log($"YouTube RSS returned {(int)response.StatusCode} for {channelId}; trying the uploads playlist.", LogSeverity.Debug);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logsService.Log($"Failed to fetch YouTube RSS for {channelId}: {ex.Message}; trying the uploads playlist.", LogSeverity.Warning);
        }

        return await FetchUploadsPlaylistAsync(channelId, ct).ConfigureAwait(false);
    }

    private async Task<(string? ChannelTitle, IReadOnlyList<VideoEntry> Entries)> FetchUploadsPlaylistAsync(
        string channelId,
        CancellationToken ct)
    {
        string uploadsPlaylistId = $"UU{channelId[2..]}";
        string url = $"https://www.youtube.com/playlist?list={Uri.EscapeDataString(uploadsPlaylistId)}";

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(BrowserUserAgent);
            request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
            using HttpResponseMessage response = await HttpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logsService.Log($"YouTube uploads playlist returned {(int)response.StatusCode} for {channelId}.", LogSeverity.Warning);
                return (null, []);
            }

            string html = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            (string? channelTitle, IReadOnlyList<VideoEntry> entries) = ParseUploadsPage(html);
            if (channelTitle == null)
                logsService.Log($"Could not parse YouTube uploads playlist for {channelId}.", LogSeverity.Warning);

            return (channelTitle, entries);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logsService.Log($"Failed to fetch YouTube uploads playlist for {channelId}: {ex.Message}", LogSeverity.Warning);
            return (null, []);
        }
    }

    internal static (string? ChannelTitle, IReadOnlyList<VideoEntry> Entries) ParseAtomFeed(string xml)
    {
        XDocument doc = XDocument.Parse(xml);
        string? channelTitle = doc.Root?.Element(Atom + "title")?.Value;

        List<VideoEntry> entries = [];
        foreach (XElement entry in doc.Descendants(Atom + "entry"))
        {
            string videoId = entry.Element(Yt + "videoId")?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(videoId))
                continue;

            string title = entry.Element(Atom + "title")?.Value ?? string.Empty;
            string link = entry.Elements(Atom + "link").FirstOrDefault()?.Attribute("href")?.Value
                          ?? $"https://www.youtube.com/watch?v={videoId}";
            string publishedText = entry.Element(Atom + "published")?.Value ?? string.Empty;
            DateTime.TryParse(publishedText, out DateTime published);
            entries.Add(new VideoEntry(videoId, title, link, published));
        }

        return (channelTitle, entries);
    }

    internal static (string? ChannelTitle, IReadOnlyList<VideoEntry> Entries) ParseUploadsPage(string html)
    {
        string? json = ExtractInitialDataJson(html);
        if (json == null)
            return (null, []);

        using JsonDocument document = JsonDocument.Parse(json);
        string? channelTitle = null;
        List<(string VideoId, string Title)> videos = [];
        TraverseInitialData(document.RootElement, ref channelTitle, videos);

        DateTime now = DateTime.UtcNow;
        VideoEntry[] entries = videos
            .DistinctBy(video => video.VideoId, StringComparer.Ordinal)
            .Select((video, index) => new VideoEntry(
                video.VideoId,
                video.Title,
                $"https://www.youtube.com/watch?v={video.VideoId}",
                now.AddMinutes(-index)))
            .ToArray();

        return (channelTitle, entries);
    }

    private static string? ExtractInitialDataJson(string html)
    {
        const string marker = "var ytInitialData = ";
        int start = html.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
            return null;

        start += marker.Length;
        int end = html.IndexOf(";</script>", start, StringComparison.Ordinal);
        if (end < 0)
            return null;

        return html[start..end];
    }

    private static void TraverseInitialData(
        JsonElement element,
        ref string? channelTitle,
        List<(string VideoId, string Title)> videos)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
                TraverseInitialData(item, ref channelTitle, videos);
            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
            return;

        if (channelTitle == null &&
            element.TryGetProperty("playlistHeaderRenderer", out JsonElement header) &&
            header.TryGetProperty("ownerText", out JsonElement ownerText))
        {
            channelTitle = ReadText(ownerText);
        }

        if (element.TryGetProperty("lockupViewModel", out JsonElement lockup) &&
            lockup.TryGetProperty("contentType", out JsonElement contentType) &&
            contentType.GetString() == "LOCKUP_CONTENT_TYPE_VIDEO" &&
            lockup.TryGetProperty("contentId", out JsonElement contentId))
        {
            string? videoId = contentId.GetString();
            string? title = null;
            if (lockup.TryGetProperty("metadata", out JsonElement metadata) &&
                metadata.TryGetProperty("lockupMetadataViewModel", out JsonElement metadataView) &&
                metadataView.TryGetProperty("title", out JsonElement titleElement))
            {
                title = ReadText(titleElement);
            }

            if (!string.IsNullOrWhiteSpace(videoId))
                videos.Add((videoId, title ?? string.Empty));
        }

        foreach (JsonProperty property in element.EnumerateObject())
            TraverseInitialData(property.Value, ref channelTitle, videos);
    }

    private static string? ReadText(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (element.TryGetProperty("content", out JsonElement content) && content.ValueKind == JsonValueKind.String)
            return content.GetString();
        if (element.TryGetProperty("simpleText", out JsonElement simpleText) && simpleText.ValueKind == JsonValueKind.String)
            return simpleText.GetString();
        if (element.TryGetProperty("runs", out JsonElement runs) && runs.ValueKind == JsonValueKind.Array)
        {
            JsonElement first = runs.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object &&
                first.TryGetProperty("text", out JsonElement text) &&
                text.ValueKind == JsonValueKind.String)
                return text.GetString();
        }

        return null;
    }
}
