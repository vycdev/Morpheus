using System.Xml.Linq;
using Discord;

namespace Morpheus.Services;

/// <summary>
/// Fetches and parses generic RSS 2.0 and Atom feeds. Handles blogs (e.g. the vycdev blog) and
/// GitHub Atom feeds (releases / commits / tags), returning a stable id + link per entry.
/// </summary>
public class RssFeedService(LogsService logsService)
{
    private static readonly HttpClient HttpClient = CreateClient();
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    private static HttpClient CreateClient()
    {
        HttpClient client = new() { Timeout = TimeSpan.FromSeconds(30) };
        // Some feeds (GitHub included) reject requests without a User-Agent.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Morpheus-Bot/1.0 (+https://github.com/vycdev/Morpheus)");
        return client;
    }

    public record FeedEntry(string EntryId, string Title, string Link, DateTime Published);

    /// <summary>
    /// Fetches a feed and returns its title + icon (if any) plus the parsed entries. Returns
    /// (null, null, empty) on any error, and null title when the feed has no title element.
    /// </summary>
    public async Task<(string? FeedTitle, string? FeedImageUrl, IReadOnlyList<FeedEntry> Entries)> FetchAsync(string feedUrl, CancellationToken ct = default)
    {
        try
        {
            string xml = await HttpClient.GetStringAsync(feedUrl, ct).ConfigureAwait(false);
            XDocument doc = XDocument.Parse(xml);

            List<FeedEntry> entries = [];

            // Atom (<entry>) first, then RSS 2.0 (<item>).
            List<XElement> atomEntries = doc.Descendants(Atom + "entry").ToList();
            if (atomEntries.Count > 0)
            {
                foreach (XElement e in atomEntries)
                {
                    string link = e.Elements(Atom + "link").FirstOrDefault(l => (string?)l.Attribute("rel") is null or "alternate")?.Attribute("href")?.Value
                                  ?? e.Elements(Atom + "link").FirstOrDefault()?.Attribute("href")?.Value
                                  ?? string.Empty;
                    string id = e.Element(Atom + "id")?.Value ?? link;
                    string title = e.Element(Atom + "title")?.Value ?? string.Empty;
                    string pubRaw = e.Element(Atom + "published")?.Value ?? e.Element(Atom + "updated")?.Value ?? string.Empty;
                    DateTime.TryParse(pubRaw, out DateTime published);

                    if (!string.IsNullOrWhiteSpace(id))
                        entries.Add(new FeedEntry(id, title, link, published));
                }
            }
            else
            {
                foreach (XElement it in doc.Descendants().Where(x => x.Name.LocalName == "item"))
                {
                    string link = it.Element("link")?.Value
                                  ?? it.Elements("link").FirstOrDefault()?.Attribute("href")?.Value
                                  ?? string.Empty;
                    string id = it.Element("guid")?.Value ?? it.Element("id")?.Value ?? link;
                    string title = it.Element("title")?.Value ?? string.Empty;
                    string pubRaw = it.Element("pubDate")?.Value ?? it.Element("published")?.Value ?? string.Empty;
                    DateTime.TryParse(pubRaw, out DateTime published);

                    if (!string.IsNullOrWhiteSpace(id))
                        entries.Add(new FeedEntry(id, title, link, published));
                }
            }

            XElement? channel = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "channel");
            string? feedTitle = doc.Root?.Element(Atom + "title")?.Value
                                ?? channel?.Element("title")?.Value
                                ?? doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "title")?.Value;

            // Atom prefers <logo> (square-ish) then <icon>; RSS uses <channel><image><url>.
            string? feedImage = doc.Root?.Element(Atom + "logo")?.Value
                                ?? doc.Root?.Element(Atom + "icon")?.Value
                                ?? channel?.Element("image")?.Element("url")?.Value;

            return (
                string.IsNullOrWhiteSpace(feedTitle) ? null : feedTitle.Trim(),
                string.IsNullOrWhiteSpace(feedImage) ? null : feedImage.Trim(),
                entries);
        }
        catch (Exception ex)
        {
            logsService.Log($"Failed to fetch RSS feed {feedUrl}: {ex.Message}", LogSeverity.Warning);
            return (null, null, []);
        }
    }
}
