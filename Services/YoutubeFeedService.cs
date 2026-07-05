using System.Xml.Linq;
using Discord;

namespace Morpheus.Services;

/// <summary>
/// Fetches and parses a YouTube channel's public uploads RSS feed
/// (https://www.youtube.com/feeds/videos.xml?channel_id=...).
/// </summary>
public class YoutubeFeedService(LogsService logsService)
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Yt = "http://www.youtube.com/xml/schemas/2015";

    public record VideoEntry(string VideoId, string Title, string Link, DateTime Published);

    public async Task<(string? ChannelTitle, IReadOnlyList<VideoEntry> Entries)> FetchFeedAsync(string channelId, CancellationToken ct = default)
    {
        string url = $"https://www.youtube.com/feeds/videos.xml?channel_id={Uri.EscapeDataString(channelId)}";
        try
        {
            string xml = await HttpClient.GetStringAsync(url, ct).ConfigureAwait(false);
            XDocument doc = XDocument.Parse(xml);

            string? channelTitle = doc.Root?.Element(Atom + "title")?.Value;

            List<VideoEntry> entries = [];
            foreach (XElement e in doc.Descendants(Atom + "entry"))
            {
                string vid = e.Element(Yt + "videoId")?.Value ?? string.Empty;
                if (string.IsNullOrEmpty(vid))
                    continue;

                string title = e.Element(Atom + "title")?.Value ?? string.Empty;
                string link = e.Elements(Atom + "link").FirstOrDefault()?.Attribute("href")?.Value
                              ?? $"https://www.youtube.com/watch?v={vid}";
                string pubRaw = e.Element(Atom + "published")?.Value ?? string.Empty;
                DateTime.TryParse(pubRaw, out DateTime published);

                entries.Add(new VideoEntry(vid, title, link, published));
            }

            return (channelTitle, entries);
        }
        catch (Exception ex)
        {
            logsService.Log($"Failed to fetch YouTube feed for {channelId}: {ex.Message}", LogSeverity.Warning);
            return (null, []);
        }
    }
}
