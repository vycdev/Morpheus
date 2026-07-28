using System.Collections.Concurrent;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Handlers;
using Morpheus.Services;
using Morpheus.Utilities;

namespace Morpheus.Modules;

[Name("Subscriptions")]
public class SubscriptionsModule : ModuleBase<SocketCommandContextExtended>
{
    private const string BrowserCustomIdPrefix = "subscriptions_browser:";
    private static readonly TimeSpan BrowserSessionLifetime = TimeSpan.FromMinutes(15);
    private static readonly ConcurrentDictionary<ulong, SubscriptionBrowserSession> BrowserSessions = new();

    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly DB db;
    private readonly WebhookService webhookService;
    private readonly YoutubeFeedService youtubeFeed;
    private readonly RssFeedService rssFeed;
    private readonly TwitchService twitch;

    public SubscriptionsModule(
        DB db,
        WebhookService webhookService,
        YoutubeFeedService youtubeFeed,
        RssFeedService rssFeed,
        TwitchService twitch,
        InteractionsHandler interactionHandler)
    {
        this.db = db;
        this.webhookService = webhookService;
        this.youtubeFeed = youtubeFeed;
        this.rssFeed = rssFeed;
        this.twitch = twitch;
        interactionHandler.RegisterInteraction("subscriptions_browser", HandleSubscriptionBrowserInteraction);
    }

    // ============================= xkcd =============================

    [Name("Set xkcd Channel")]
    [Summary("Posts every new xkcd comic in the given channel (defaults to the current one). Creates a webhook if needed.")]
    [Command("setxkcdchannel")]
    [Alias("setxkcd", "xkcdchannel", "addxkcd")]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.ManageWebhooks)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(2, 20)]
    public async Task SetXkcdChannelAsync(ITextChannel? channel = null)
    {
        ITextChannel? target = channel ?? Context.Channel as ITextChannel;
        if (target == null)
        {
            await ReplyAsync("Please use this in (or specify) a normal text channel.");
            return;
        }

        XkcdSubscription? existing = await db.XkcdSubscriptions.FirstOrDefaultAsync(s => s.ChannelDiscordId == target.Id);
        if (existing != null)
        {
            await ReplyAsync($"xkcd comics are already being posted in <#{target.Id}>.");
            return;
        }

        Webhook? webhook = await webhookService.GetOrCreateWebhookAsync(target);
        if (webhook == null)
        {
            await ReplyAsync($"I couldn't create a webhook in <#{target.Id}>. Make sure I have the **Manage Webhooks** permission there (and that the channel isn't at Discord's 15-webhook limit).");
            return;
        }

        db.XkcdSubscriptions.Add(new XkcdSubscription
        {
            GuildDiscordId = target.GuildId,
            ChannelDiscordId = target.Id,
            WebhookId = webhook.Id,
            InsertDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        await ReplyAsync($"Done! New xkcd comics will now be posted in <#{target.Id}>.");
    }

    [Name("Remove xkcd Channel")]
    [Summary("Stops posting xkcd comics in the given channel (defaults to the current one).")]
    [Command("removexkcdchannel")]
    [Alias("removexkcd", "unsetxkcd", "delxkcd")]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(2, 20)]
    public async Task RemoveXkcdChannelAsync(ITextChannel? channel = null)
    {
        ITextChannel? target = channel ?? Context.Channel as ITextChannel;
        if (target == null)
        {
            await ReplyAsync("Please use this in (or specify) a normal text channel.");
            return;
        }

        XkcdSubscription? existing = await db.XkcdSubscriptions.FirstOrDefaultAsync(s => s.ChannelDiscordId == target.Id);
        if (existing == null)
        {
            await ReplyAsync($"xkcd comics aren't being posted in <#{target.Id}>.");
            return;
        }

        db.XkcdSubscriptions.Remove(existing);
        await db.SaveChangesAsync();

        await ReplyAsync($"Stopped posting xkcd comics in <#{target.Id}>.");
    }

    // ============================ YouTube ============================

    [Name("Subscribe to YouTube channels")]
    [Summary("Posts new videos from one or more YouTube channels. Separate channel URLs, @handles, or ids with spaces/newlines, or attach a text file. Optionally pass a target Discord channel; otherwise the current channel is used.")]
    [Command("subscribeyoutube")]
    [Alias("ytsubscribe", "ytsub", "subyt", "subscribeyt")]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.ManageWebhooks)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 30)]
    public async Task SubscribeYoutubeAsync([Remainder] string? input = null)
    {
        string bulkInput;
        try
        {
            bulkInput = await CollectBulkInputAsync(input);
        }
        catch (InvalidOperationException ex)
        {
            await ReplyAsync(ex.Message);
            return;
        }

        SubscriptionInputParser.SourceList parsed = SubscriptionInputParser.ParseSources(bulkInput);
        ITextChannel? target = parsed.ChannelId.HasValue
            ? Context.Guild.GetTextChannel(parsed.ChannelId.Value)
            : Context.Channel as ITextChannel;
        if (target == null)
        {
            await ReplyAsync("Please use this in (or specify) a normal text channel.");
            return;
        }

        if (parsed.Sources.Count == 0)
        {
            await ReplyAsync("Provide one or more YouTube channel URLs, @handles, or channel ids, or attach a text file containing them.");
            return;
        }

        if (parsed.Sources.Count > 200)
        {
            await ReplyAsync("A single bulk command can contain at most 200 YouTube channels.");
            return;
        }

        Webhook? webhook = await webhookService.GetOrCreateWebhookAsync(target);
        if (webhook == null)
        {
            await ReplyAsync($"I couldn't create a webhook in <#{target.Id}>. Make sure I have the **Manage Webhooks** permission there (and that the channel isn't at Discord's 15-webhook limit).");
            return;
        }

        using IDisposable typing = Context.Channel.EnterTypingState();
        List<BulkSubscribeResult> results = [];
        foreach (string source in parsed.Sources)
        {
            try
            {
                results.Add(await SubscribeYoutubeSourceAsync(source, target, webhook));
            }
            catch (Exception ex)
            {
                results.Add(BulkSubscribeResult.Failed(source, ex.Message));
            }
        }

        await ReplyBulkSummaryAsync("YouTube", target, results);
    }

    [Name("Unsubscribe from a YouTuber")]
    [Summary("Stops posting a YouTuber's videos in a channel. Accepts a channel URL, @handle, or channel id.")]
    [Command("unsubscribeyoutube")]
    [Alias("ytunsubscribe", "ytunsub", "unsubyt", "unsubscribeyt")]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 30)]
    public async Task UnsubscribeYoutubeAsync(string youtubeChannel, ITextChannel? channel = null)
    {
        ITextChannel? target = channel ?? Context.Channel as ITextChannel;
        if (target == null)
        {
            await ReplyAsync("Please use this in (or specify) a normal text channel.");
            return;
        }

        string? youtubeChannelId = await YoutubeUtils.ResolveChannelIdAsync(HttpClient, youtubeChannel);

        // Fall back to matching by stored title if the reference can't be resolved to an id.
        YoutubeSubscription? existing = youtubeChannelId != null
            ? await db.YoutubeSubscriptions.FirstOrDefaultAsync(s => s.ChannelDiscordId == target.Id && s.YoutubeChannelId == youtubeChannelId)
            : await db.YoutubeSubscriptions.FirstOrDefaultAsync(s => s.ChannelDiscordId == target.Id && s.YoutubeChannelTitle.ToLower() == youtubeChannel.ToLower());

        if (existing == null)
        {
            await ReplyAsync($"There's no matching YouTube subscription in <#{target.Id}>.");
            return;
        }

        string title = existing.YoutubeChannelTitle;
        db.YoutubeSubscriptions.Remove(existing);
        await db.SaveChangesAsync();

        await ReplyAsync($"Unsubscribed from **{title}** in <#{target.Id}>.");
    }

    // ========================= Generic RSS/Atom =========================

    [Name("Subscribe to RSS/Atom feeds")]
    [Summary("Posts new entries from one or more RSS or Atom feeds in this channel. Separate URLs with spaces/newlines, or attach a text file. A one-feed command can still include a display name; bulk files can use `URL | Display name`.")]
    [Command("subscriberss")]
    [Alias("rsssubscribe", "subrss", "addfeed", "subscribefeed")]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.ManageWebhooks)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 30)]
    public async Task SubscribeRssAsync([Remainder] string? input = null)
    {
        string bulkInput;
        try
        {
            bulkInput = await CollectBulkInputAsync(input);
        }
        catch (InvalidOperationException ex)
        {
            await ReplyAsync(ex.Message);
            return;
        }

        IReadOnlyList<SubscriptionInputParser.RssSource> sources = SubscriptionInputParser.ParseRssSources(bulkInput);
        ITextChannel? target = Context.Channel as ITextChannel;
        if (target == null)
        {
            await ReplyAsync("Please use this in a normal text channel.");
            return;
        }

        if (sources.Count == 0)
        {
            await ReplyAsync("Provide one or more RSS/Atom feed URLs, or attach a text file containing them.");
            return;
        }

        if (sources.Count > 200)
        {
            await ReplyAsync("A single bulk command can contain at most 200 RSS/Atom feeds.");
            return;
        }

        Webhook? webhook = await webhookService.GetOrCreateWebhookAsync(target);
        if (webhook == null)
        {
            await ReplyAsync($"I couldn't create a webhook in <#{target.Id}>. Make sure I have the **Manage Webhooks** permission there (and that the channel isn't at Discord's 15-webhook limit).");
            return;
        }

        using IDisposable typing = Context.Channel.EnterTypingState();
        List<BulkSubscribeResult> results = [];
        foreach (SubscriptionInputParser.RssSource source in sources)
        {
            try
            {
                results.Add(await SubscribeRssSourceAsync(source, target, webhook));
            }
            catch (Exception ex)
            {
                results.Add(BulkSubscribeResult.Failed(source.Url, ex.Message));
            }
        }

        await ReplyBulkSummaryAsync("RSS/Atom", target, results);
    }

    [Name("Unsubscribe from an RSS/Atom feed")]
    [Summary("Stops posting an RSS/Atom feed in this channel.")]
    [Command("unsubscriberss")]
    [Alias("rssunsubscribe", "unsubrss", "removefeed", "delfeed")]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 30)]
    public async Task UnsubscribeRssAsync(string feedUrl)
    {
        ITextChannel? target = Context.Channel as ITextChannel;
        if (target == null)
        {
            await ReplyAsync("Please use this in a normal text channel.");
            return;
        }

        string fragmentlessFeedUrl = SubscriptionInputParser.RemoveRssFragment(feedUrl);
        string normalized = Uri.TryCreate(fragmentlessFeedUrl, UriKind.Absolute, out Uri? uri) ? uri.ToString() : fragmentlessFeedUrl;
        string legacyFeedPattern = EscapeLikePattern(normalized) + "#%";

        RssSubscription? existing = await db.RssSubscriptions
            .FirstOrDefaultAsync(s => s.ChannelDiscordId == target.Id &&
                (s.FeedUrl == normalized || EF.Functions.Like(s.FeedUrl, legacyFeedPattern, "\\")));
        if (existing == null)
        {
            await ReplyAsync($"That feed isn't being posted in <#{target.Id}>.");
            return;
        }

        string name = existing.DisplayName;
        db.RssSubscriptions.Remove(existing);
        await db.SaveChangesAsync();

        await ReplyAsync($"Stopped posting **{name}** in <#{target.Id}>.");
    }

    // ============================= Twitch =============================

    [Name("Subscribe to Twitch streamers")]
    [Summary("Posts go-live notifications for one or more Twitch streamers. Separate handles or URLs with spaces/newlines, or attach a text file. Optionally pass a target Discord channel; otherwise the current channel is used.")]
    [Command("subscribetwitch")]
    [Alias("twitchsubscribe", "twitchsub", "subtwitch")]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.ManageWebhooks)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 30)]
    public async Task SubscribeTwitchAsync([Remainder] string? input = null)
    {
        string bulkInput;
        try
        {
            bulkInput = await CollectBulkInputAsync(input);
        }
        catch (InvalidOperationException ex)
        {
            await ReplyAsync(ex.Message);
            return;
        }

        SubscriptionInputParser.SourceList parsed = SubscriptionInputParser.ParseSources(bulkInput);
        ITextChannel? target = parsed.ChannelId.HasValue
            ? Context.Guild.GetTextChannel(parsed.ChannelId.Value)
            : Context.Channel as ITextChannel;
        if (target == null)
        {
            await ReplyAsync("Please use this in (or specify) a normal text channel.");
            return;
        }

        if (parsed.Sources.Count == 0)
        {
            await ReplyAsync("Provide one or more Twitch handles or channel URLs, or attach a text file containing them.");
            return;
        }

        if (parsed.Sources.Count > 200)
        {
            await ReplyAsync("A single bulk command can contain at most 200 Twitch streamers.");
            return;
        }

        if (!twitch.IsConfigured)
        {
            await ReplyAsync("Twitch notifications aren't configured on this bot (missing `TWITCH_CLIENT_ID` / `TWITCH_CLIENT_SECRET`).");
            return;
        }

        Webhook? webhook = await webhookService.GetOrCreateWebhookAsync(target);
        if (webhook == null)
        {
            await ReplyAsync($"I couldn't create a webhook in <#{target.Id}>. Make sure I have the **Manage Webhooks** permission there (and that the channel isn't at Discord's 15-webhook limit).");
            return;
        }

        using IDisposable typing = Context.Channel.EnterTypingState();
        List<BulkSubscribeResult> results = [];
        foreach (string source in parsed.Sources)
        {
            try
            {
                results.Add(await SubscribeTwitchSourceAsync(source, target, webhook));
            }
            catch (Exception ex)
            {
                results.Add(BulkSubscribeResult.Failed(source, ex.Message));
            }
        }

        await ReplyBulkSummaryAsync("Twitch", target, results);
    }

    [Name("Unsubscribe from a Twitch streamer")]
    [Summary("Stops posting go-live notifications for a Twitch streamer. Accepts a Twitch login/handle or channel URL.")]
    [Command("unsubscribetwitch")]
    [Alias("twitchunsubscribe", "twitchunsub", "unsubtwitch")]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 30)]
    public async Task UnsubscribeTwitchAsync(string streamer, ITextChannel? channel = null)
    {
        ITextChannel? target = channel ?? Context.Channel as ITextChannel;
        if (target == null)
        {
            await ReplyAsync("Please use this in (or specify) a normal text channel.");
            return;
        }

        string login = ExtractTwitchLogin(streamer);
        TwitchSubscription? existing = await db.TwitchSubscriptions
            .FirstOrDefaultAsync(s => s.ChannelDiscordId == target.Id && s.TwitchLogin == login);
        if (existing == null)
        {
            await ReplyAsync($"There's no Twitch go-live notification for `{login}` in <#{target.Id}>.");
            return;
        }

        string name = existing.TwitchDisplayName;
        db.TwitchSubscriptions.Remove(existing);
        await db.SaveChangesAsync();

        await ReplyAsync($"Stopped go-live notifications for **{name}** in <#{target.Id}>.");
    }

    [Name("List Subscriptions")]
    [Summary("Opens an interactive, paginated browser for all xkcd, YouTube, RSS and Twitch subscriptions configured in this server.")]
    [Command("subscriptions")]
    [Alias("listsubscriptions", "listsubs", "feeds", "ytsubs")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(2, 15)]
    public async Task ListSubscriptionsAsync()
    {
        ulong guildId = Context.Guild.Id;

        List<XkcdSubscription> xkcd = await db.XkcdSubscriptions
            .Where(s => s.GuildDiscordId == guildId)
            .ToListAsync();

        List<YoutubeSubscription> youtube = await db.YoutubeSubscriptions
            .Where(s => s.GuildDiscordId == guildId)
            .OrderBy(s => s.YoutubeChannelTitle)
            .ToListAsync();

        List<RssSubscription> rss = await db.RssSubscriptions
            .Where(s => s.GuildDiscordId == guildId)
            .OrderBy(s => s.DisplayName)
            .ToListAsync();

        List<TwitchSubscription> twitchSubs = await db.TwitchSubscriptions
            .Where(s => s.GuildDiscordId == guildId)
            .OrderBy(s => s.TwitchDisplayName)
            .ToListAsync();

        if (xkcd.Count == 0 && youtube.Count == 0 && rss.Count == 0 && twitchSubs.Count == 0)
        {
            await ReplyAsync("There are no feed subscriptions in this server yet. Use `setxkcdchannel`, `subscribeyoutube`, `subscriberss` or `subscribetwitch` to add one.");
            return;
        }

        List<SubscriptionBrowserItem> items =
        [
            .. xkcd.Select(subscription => new SubscriptionBrowserItem(
                SubscriptionFeedType.Xkcd,
                "xkcd",
                subscription.ChannelDiscordId,
                "https://xkcd.com/")),
            .. youtube.Select(subscription => new SubscriptionBrowserItem(
                SubscriptionFeedType.Youtube,
                subscription.YoutubeChannelTitle,
                subscription.ChannelDiscordId,
                $"https://www.youtube.com/channel/{subscription.YoutubeChannelId}")),
            .. rss.Select(subscription => new SubscriptionBrowserItem(
                SubscriptionFeedType.Rss,
                subscription.DisplayName,
                subscription.ChannelDiscordId,
                subscription.FeedUrl)),
            .. twitchSubs.Select(subscription => new SubscriptionBrowserItem(
                SubscriptionFeedType.Twitch,
                subscription.TwitchDisplayName,
                subscription.ChannelDiscordId,
                $"https://www.twitch.tv/{subscription.TwitchLogin}"))
        ];

        CleanupExpiredBrowserSessions();
        SubscriptionBrowserSession session = new(Context.User.Id, Context.Guild.Id, items);
        (Embed embed, MessageComponent components) = BuildSubscriptionBrowserView(session);
        IUserMessage message = await ReplyAsync(embed: embed, components: components);
        BrowserSessions[message.Id] = session;
    }

    private static async Task HandleSubscriptionBrowserInteraction(SocketInteraction interaction)
    {
        if (interaction is not SocketMessageComponent component)
            return;

        string customId = component.Data.CustomId ?? string.Empty;
        if (!customId.StartsWith(BrowserCustomIdPrefix, StringComparison.Ordinal))
            return;

        if (!BrowserSessions.TryGetValue(component.Message.Id, out SubscriptionBrowserSession? session))
        {
            await component.RespondAsync("This subscription browser has expired. Run `subscriptions` again.", ephemeral: true);
            return;
        }

        if (component.User.Id != session.UserId)
        {
            await component.RespondAsync("Only the person who opened this subscription browser can use its buttons.", ephemeral: true);
            return;
        }

        if (component.GuildId != session.GuildId)
        {
            await component.RespondAsync("This subscription browser belongs to a different server.", ephemeral: true);
            return;
        }

        if (DateTime.UtcNow - session.LastTouchedAt > BrowserSessionLifetime)
        {
            BrowserSessions.TryRemove(component.Message.Id, out _);
            await component.RespondAsync("This subscription browser has expired. Run `subscriptions` again.", ephemeral: true);
            return;
        }

        string action = customId[BrowserCustomIdPrefix.Length..];
        if (action == "close")
        {
            BrowserSessions.TryRemove(component.Message.Id, out _);
            await component.DeferAsync();
            await component.Message.ModifyAsync(properties => properties.Components = new ComponentBuilder().Build());
            return;
        }

        Embed embed;
        MessageComponent components;
        lock (session)
        {
            if (action == "previous")
                session.PageIndex--;
            else if (action == "next")
                session.PageIndex++;
            else if (action.StartsWith("filter:", StringComparison.Ordinal) &&
                     Enum.TryParse(action["filter:".Length..], ignoreCase: true, out SubscriptionFeedType filter))
            {
                session.Filter = filter;
                session.PageIndex = 0;
            }

            session.LastTouchedAt = DateTime.UtcNow;
            (embed, components) = BuildSubscriptionBrowserView(session);
        }

        await component.DeferAsync();
        await component.Message.ModifyAsync(properties =>
        {
            properties.Embed = embed;
            properties.Components = components;
        });
    }

    private static (Embed Embed, MessageComponent Components) BuildSubscriptionBrowserView(SubscriptionBrowserSession session)
    {
        SubscriptionBrowserPage page = SubscriptionBrowser.GetPage(session.Items, session.Filter, session.PageIndex);
        session.PageIndex = page.PageIndex;

        string counts = string.Join("  •  ", new[]
        {
            $"**{SubscriptionBrowser.Count(session.Items, SubscriptionFeedType.All)}** total",
            $"YouTube {SubscriptionBrowser.Count(session.Items, SubscriptionFeedType.Youtube)}",
            $"RSS {SubscriptionBrowser.Count(session.Items, SubscriptionFeedType.Rss)}",
            $"Twitch {SubscriptionBrowser.Count(session.Items, SubscriptionFeedType.Twitch)}",
            $"xkcd {SubscriptionBrowser.Count(session.Items, SubscriptionFeedType.Xkcd)}"
        });

        string entries = page.Items.Count == 0
            ? "*No subscriptions in this category.*"
            : string.Join("\n\n", page.Items.Select((item, index) => FormatBrowserItem(item, page.FirstItemNumber + index)));

        Embed embed = new EmbedBuilder()
            .WithColor(Utilities.Colors.Blue)
            .WithTitle($"Feed subscriptions — {SubscriptionBrowser.DisplayName(page.Filter)}")
            .WithDescription($"{counts}\n\n{entries}")
            .WithFooter($"Page {page.PageIndex + 1}/{page.TotalPages} • Showing {page.FirstItemNumber}–{page.LastItemNumber} of {page.TotalItems} • Controls expire after 15 minutes")
            .Build();

        ComponentBuilder componentBuilder = new();
        foreach (SubscriptionFeedType feedType in Enum.GetValues<SubscriptionFeedType>())
        {
            int count = SubscriptionBrowser.Count(session.Items, feedType);
            componentBuilder.WithButton(
                $"{SubscriptionBrowser.DisplayName(feedType)} {count}",
                $"{BrowserCustomIdPrefix}filter:{feedType.ToString().ToLowerInvariant()}",
                feedType == page.Filter ? ButtonStyle.Primary : ButtonStyle.Secondary,
                disabled: count == 0,
                row: 0);
        }

        componentBuilder
            .WithButton("◀ Previous", $"{BrowserCustomIdPrefix}previous", ButtonStyle.Secondary, disabled: page.PageIndex == 0, row: 1)
            .WithButton("Next ▶", $"{BrowserCustomIdPrefix}next", ButtonStyle.Secondary, disabled: page.PageIndex >= page.TotalPages - 1, row: 1)
            .WithButton("Close", $"{BrowserCustomIdPrefix}close", ButtonStyle.Danger, row: 1);

        return (embed, componentBuilder.Build());
    }

    private static string FormatBrowserItem(SubscriptionBrowserItem item, int number)
    {
        string icon = item.FeedType switch
        {
            SubscriptionFeedType.Youtube => "▶️",
            SubscriptionFeedType.Rss => "📰",
            SubscriptionFeedType.Twitch => "🟣",
            SubscriptionFeedType.Xkcd => "💬",
            _ => "•"
        };
        string name = EscapeBrowserText(item.Name, 80);
        string sourceLabel = item.FeedType switch
        {
            SubscriptionFeedType.Youtube => "Open YouTube channel",
            SubscriptionFeedType.Rss => "Open feed",
            SubscriptionFeedType.Twitch => "Open Twitch channel",
            SubscriptionFeedType.Xkcd => "Open xkcd",
            _ => "Open source"
        };
        string source = string.IsNullOrWhiteSpace(item.SourceUrl) || item.SourceUrl.Length > 200
            ? string.Empty
            : $" • [{sourceLabel}]({item.SourceUrl.Replace(")", "%29", StringComparison.Ordinal)})";

        return $"`{number}.` {icon} **{name}**\n   <#{item.ChannelId}>{source}";
    }

    private static string EscapeBrowserText(string value, int maxLength)
    {
        string escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("~", "\\~", StringComparison.Ordinal)
            .Replace("`", "'", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return escaped.Length <= maxLength ? escaped : escaped[..(maxLength - 1)] + "…";
    }

    private static void CleanupExpiredBrowserSessions()
    {
        DateTime cutoff = DateTime.UtcNow - BrowserSessionLifetime;
        foreach ((ulong messageId, SubscriptionBrowserSession session) in BrowserSessions)
        {
            if (session.LastTouchedAt < cutoff)
                BrowserSessions.TryRemove(messageId, out _);
        }
    }

    private sealed class SubscriptionBrowserSession(ulong userId, ulong guildId, IReadOnlyList<SubscriptionBrowserItem> items)
    {
        public ulong UserId { get; } = userId;
        public ulong GuildId { get; } = guildId;
        public IReadOnlyList<SubscriptionBrowserItem> Items { get; } = items;
        public SubscriptionFeedType Filter { get; set; } = SubscriptionFeedType.All;
        public int PageIndex { get; set; }
        public DateTime LastTouchedAt { get; set; } = DateTime.UtcNow;
    }

    private async Task<BulkSubscribeResult> SubscribeYoutubeSourceAsync(string source, ITextChannel target, Webhook webhook)
    {
        string? youtubeChannelId = await YoutubeUtils.ResolveChannelIdAsync(HttpClient, source);
        if (string.IsNullOrEmpty(youtubeChannelId))
            return BulkSubscribeResult.Failed(source, "Channel could not be resolved.");

        (string? channelTitle, IReadOnlyList<YoutubeFeedService.VideoEntry> entries) = await youtubeFeed.FetchFeedAsync(youtubeChannelId);
        if (channelTitle == null)
            return BulkSubscribeResult.Failed(source, "Uploads feed could not be read.");

        bool exists = await db.YoutubeSubscriptions
            .AnyAsync(subscription => subscription.ChannelDiscordId == target.Id && subscription.YoutubeChannelId == youtubeChannelId);
        if (exists)
            return BulkSubscribeResult.Already(source, channelTitle);

        string? avatarUrl = await YoutubeUtils.GetChannelAvatarAsync(HttpClient, youtubeChannelId);
        YoutubeSubscription subscription = new()
        {
            GuildDiscordId = target.GuildId,
            ChannelDiscordId = target.Id,
            YoutubeChannelId = youtubeChannelId,
            YoutubeChannelTitle = channelTitle,
            YoutubeAvatarUrl = avatarUrl,
            WebhookId = webhook.Id,
            InsertDate = DateTime.UtcNow
        };
        db.YoutubeSubscriptions.Add(subscription);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            db.Entry(subscription).State = EntityState.Detached;
            return BulkSubscribeResult.Already(source, channelTitle);
        }
        catch
        {
            db.Entry(subscription).State = EntityState.Detached;
            throw;
        }

        await SeedSeenVideosBestEffortAsync(youtubeChannelId, entries);
        return BulkSubscribeResult.Subscribed(source, channelTitle);
    }

    private async Task<BulkSubscribeResult> SubscribeRssSourceAsync(
        SubscriptionInputParser.RssSource source,
        ITextChannel target,
        Webhook webhook)
    {
        string fragmentlessFeedUrl = SubscriptionInputParser.RemoveRssFragment(source.Url);
        if (!Uri.TryCreate(fragmentlessFeedUrl, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return BulkSubscribeResult.Failed(source.Url, "URL must start with http:// or https://.");

        string feedUrl = uri.ToString();
        (string? feedTitle, string? feedImage, IReadOnlyList<RssFeedService.FeedEntry> entries) = await rssFeed.FetchAsync(feedUrl);
        if (feedTitle == null && entries.Count == 0)
            return BulkSubscribeResult.Failed(source.Url, "Feed could not be read.");

        string legacyFeedPattern = EscapeLikePattern(feedUrl) + "#%";
        bool exists = await db.RssSubscriptions
            .AnyAsync(subscription => subscription.ChannelDiscordId == target.Id &&
                (subscription.FeedUrl == feedUrl || EF.Functions.Like(subscription.FeedUrl, legacyFeedPattern, "\\")));
        if (exists)
            return BulkSubscribeResult.Already(source.Url, feedTitle ?? uri.Host);

        string name = !string.IsNullOrWhiteSpace(source.DisplayName) ? source.DisplayName.Trim()
            : !string.IsNullOrWhiteSpace(feedTitle) ? feedTitle
            : uri.Host;
        if (name.Length > 80)
            name = name[..80];

        RssSubscription subscription = new()
        {
            GuildDiscordId = target.GuildId,
            ChannelDiscordId = target.Id,
            FeedUrl = feedUrl,
            DisplayName = name,
            AvatarUrl = feedImage,
            WebhookId = webhook.Id,
            InsertDate = DateTime.UtcNow
        };
        db.RssSubscriptions.Add(subscription);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            db.Entry(subscription).State = EntityState.Detached;
            return BulkSubscribeResult.Already(source.Url, name);
        }
        catch
        {
            db.Entry(subscription).State = EntityState.Detached;
            throw;
        }

        await SeedSeenRssBestEffortAsync(feedUrl, entries);
        return BulkSubscribeResult.Subscribed(source.Url, name);
    }

    private async Task<BulkSubscribeResult> SubscribeTwitchSourceAsync(string source, ITextChannel target, Webhook webhook)
    {
        string login = ExtractTwitchLogin(source);
        TwitchService.TwitchUser? user = await twitch.GetUserAsync(login);
        if (user == null)
            return BulkSubscribeResult.Failed(source, $"Twitch channel `{login}` was not found.");

        bool exists = await db.TwitchSubscriptions
            .AnyAsync(subscription => subscription.ChannelDiscordId == target.Id && subscription.TwitchUserId == user.Id);
        if (exists)
            return BulkSubscribeResult.Already(source, user.DisplayName);

        IReadOnlyDictionary<string, TwitchService.TwitchStream> live = await twitch.GetLiveStreamsAsync([user.Id]);
        live.TryGetValue(user.Id, out TwitchService.TwitchStream? currentStream);

        TwitchSubscription subscription = new()
        {
            GuildDiscordId = target.GuildId,
            ChannelDiscordId = target.Id,
            TwitchUserId = user.Id,
            TwitchLogin = user.Login,
            TwitchDisplayName = user.DisplayName,
            AvatarUrl = user.ProfileImageUrl,
            IsLive = currentStream != null,
            LastAnnouncedStreamId = currentStream?.Id,
            WebhookId = webhook.Id,
            InsertDate = DateTime.UtcNow
        };
        db.TwitchSubscriptions.Add(subscription);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            db.Entry(subscription).State = EntityState.Detached;
            return BulkSubscribeResult.Already(source, user.DisplayName);
        }
        catch
        {
            db.Entry(subscription).State = EntityState.Detached;
            throw;
        }

        return BulkSubscribeResult.Subscribed(source, user.DisplayName);
    }

    private async Task<string> CollectBulkInputAsync(string? input)
    {
        List<string> parts = [];
        if (!string.IsNullOrWhiteSpace(input))
            parts.Add(input);

        foreach (Attachment attachment in Context.Message.Attachments)
        {
            if (attachment.Size > 256 * 1024)
                throw new InvalidOperationException($"Attachment `{attachment.Filename}` is too large. Bulk-list files must be 256 KB or smaller.");

            string extension = Path.GetExtension(attachment.Filename);
            if (!extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Attachment `{attachment.Filename}` must be a .txt or .csv file.");

            parts.Add(await HttpClient.GetStringAsync(attachment.Url));
        }

        return string.Join('\n', parts);
    }

    private async Task ReplyBulkSummaryAsync(string sourceType, ITextChannel target, IReadOnlyList<BulkSubscribeResult> results)
    {
        int subscribed = results.Count(result => result.Status == BulkSubscribeStatus.Subscribed);
        int already = results.Count(result => result.Status == BulkSubscribeStatus.AlreadySubscribed);
        int failed = results.Count(result => result.Status == BulkSubscribeStatus.Failed);
        string response = $"{sourceType} subscription finished for <#{target.Id}>: **{subscribed} subscribed**, **{already} already subscribed**, **{failed} failed**.";

        foreach (BulkSubscribeResult failure in results.Where(result => result.Status == BulkSubscribeStatus.Failed))
        {
            string line = $"\n- `{ClampSummaryText(failure.Source, 100)}`: {ClampSummaryText(failure.Detail ?? "Unknown error", 180)}";
            if (response.Length + line.Length > 1900)
            {
                response += "\n- Additional failures omitted; fix the listed inputs, then retry the same bulk command.";
                break;
            }

            response += line;
        }

        await ReplyAsync(response);
    }

    private static string ClampSummaryText(string value, int maxLength)
    {
        string sanitized = value.Replace('`', '\'').Replace('\r', ' ').Replace('\n', ' ').Trim();
        return sanitized.Length <= maxLength ? sanitized : sanitized[..(maxLength - 1)] + "…";
    }

    private enum BulkSubscribeStatus
    {
        Subscribed,
        AlreadySubscribed,
        Failed
    }

    private sealed record BulkSubscribeResult(string Source, string Label, BulkSubscribeStatus Status, string? Detail = null)
    {
        public static BulkSubscribeResult Subscribed(string source, string label) => new(source, label, BulkSubscribeStatus.Subscribed);
        public static BulkSubscribeResult Already(string source, string label) => new(source, label, BulkSubscribeStatus.AlreadySubscribed);
        public static BulkSubscribeResult Failed(string source, string detail) => new(source, source, BulkSubscribeStatus.Failed, detail);
    }

    private async Task SeedSeenRssBestEffortAsync(string feedUrl, IReadOnlyList<RssFeedService.FeedEntry> entries)
    {
        if (entries.Count == 0)
            return;

        List<string> ids = entries.Select(e => e.EntryId).ToList();
        HashSet<string> alreadySeen = (await db.RssSeenEntries
                .Where(v => v.FeedUrl == feedUrl && ids.Contains(v.EntryId))
                .Select(v => v.EntryId)
                .ToListAsync())
            .ToHashSet();

        List<RssSeenEntry> added = [];
        foreach (RssFeedService.FeedEntry entry in entries)
        {
            if (alreadySeen.Contains(entry.EntryId))
                continue;

            RssSeenEntry row = new() { FeedUrl = feedUrl, EntryId = entry.EntryId, SeenAt = DateTime.UtcNow };
            db.RssSeenEntries.Add(row);
            added.Add(row);
        }

        if (added.Count == 0)
            return;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            foreach (RssSeenEntry row in added)
                db.Entry(row).State = EntityState.Detached;
        }
    }

    internal static string ExtractTwitchLogin(string input)
    {
        input = input.Trim();

        // Accept a full channel URL like https://twitch.tv/name or twitch.tv/name
        int idx = input.IndexOf("twitch.tv/", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            input = input[(idx + "twitch.tv/".Length)..];

        // Strip any leading @, trailing slash, query, or fragment, and lowercase.
        input = input.TrimStart('@').Trim('/');
        int delimiter = input.IndexOfAny(['/', '?', '#']);
        if (delimiter >= 0)
            input = input[..delimiter];

        return input.ToLowerInvariant();
    }

    internal static string EscapeLikePattern(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private async Task SeedSeenVideosBestEffortAsync(string youtubeChannelId, IReadOnlyList<YoutubeFeedService.VideoEntry> entries)
    {
        if (entries.Count == 0)
            return;

        List<string> ids = entries.Select(e => e.VideoId).ToList();
        HashSet<string> alreadySeen = (await db.YoutubeSeenVideos
                .Where(v => ids.Contains(v.VideoId))
                .Select(v => v.VideoId)
                .ToListAsync())
            .ToHashSet();

        List<YoutubeSeenVideo> added = [];
        foreach (YoutubeFeedService.VideoEntry entry in entries)
        {
            if (alreadySeen.Contains(entry.VideoId))
                continue;

            YoutubeSeenVideo row = new()
            {
                YoutubeChannelId = youtubeChannelId,
                VideoId = entry.VideoId,
                SeenAt = DateTime.UtcNow
            };
            db.YoutubeSeenVideos.Add(row);
            added.Add(row);
        }

        if (added.Count == 0)
            return;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A concurrent job run already recorded one of these videos as seen. Detach our
            // pending inserts so the context stays usable; the job's seen-tracking is authoritative.
            foreach (YoutubeSeenVideo row in added)
                db.Entry(row).State = EntityState.Detached;
        }
    }
}
