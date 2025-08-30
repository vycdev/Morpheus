using Discord.Commands;
using Discord;
using Discord.WebSocket;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;
using Microsoft.EntityFrameworkCore;
using Morpheus.Utilities.Lists;
using System.IO.Hashing;
using System.Text;
using Morpheus.Utilities.Text;

namespace Morpheus.Handlers;

public class ActivityHandler
{
    private readonly DiscordSocketClient client;
    private readonly CommandService commands;
    private readonly IServiceProvider serviceProvider;
    private readonly GuildService guildService;
    private readonly UsersService usersService;
    private readonly DB dbContext;
    private readonly RandomBag happyEmojisBag = new(EmojiList.EmojisHappy);
    private readonly bool started = false;

    public ActivityHandler(DiscordSocketClient client, CommandService commands, IServiceProvider serviceProvider, GuildService guildService, UsersService usersService, DB db)
    {
        if (started)
            throw new InvalidOperationException("At most one instance of this service can be started");

        started = true;

        this.client = client;
        this.commands = commands;
        this.serviceProvider = serviceProvider;
        this.guildService = guildService;
        this.usersService = usersService;
        this.dbContext = db;

        client.MessageReceived += HandleActivity;
    }

    private async Task HandleActivity(SocketMessage messageParam)
    {
        // Don't process the command if it was a system message
        if (messageParam is not SocketUserMessage message)
            return;

        // Check if message was sent by a bot or webhook
        if (message.Author.IsBot || message.Author.IsWebhook)
            return;

        // Ignore messages not in guilds
        if (message.Channel is not SocketGuildChannel guildChannel)
            return;

        User user = await usersService.TryGetCreateUser(message.Author);
        await usersService.TryUpdateUsername(message.Author, user);

        // If the guild doesn't exist, create it and then get it
        Guild guild = await guildService.TryGetCreateGuild(guildChannel.Guild);

        string messageHash = Convert.ToBase64String(XxHash64.Hash(Encoding.UTF8.GetBytes(message.Content)));
        // Compute SimHash over normalized trigrams (non-reversible)
        (ulong simHash, int normLen) = SimHasher.ComputeSimHash(message.Content);
        UserActivity? previousUserActivityInGuild = dbContext.UserActivity
            .Where(ua => ua.UserId == user.Id && ua.GuildId == guild.Id)
            .OrderByDescending(ua => ua.InsertDate)
            .FirstOrDefault();

        // Fetch last 10 user activities for similarity checks
        var lastTen = dbContext.UserActivity
            .Where(ua => ua.UserId == user.Id && ua.GuildId == guild.Id)
            .OrderByDescending(ua => ua.InsertDate)
            .Select(ua => new { ua.MessageSimHash, ua.NormalizedLength, ua.InsertDate })
            .Take(10)
            .ToList();

        UserActivity? previousActivityInGuild = dbContext.UserActivity
            .Where(ua => ua.GuildId == guild.Id)
            .OrderByDescending(ua => ua.InsertDate)
            .FirstOrDefault();

        // Calculate XP 
        DateTime now = DateTime.UtcNow;

        // Base XP for sending a message
        int baseXP = 1;
        // Scale XP based on message length compared to guild average
        double messageLengthXp = message.Content.Length / (previousActivityInGuild?.GuildAverageMessageLength * 0.1) ?? 1;
        // If the message hash is the same as the previous message and sent within 30 seconds, no XP is gained
        int similarityPenaltySimple = (previousUserActivityInGuild?.MessageHash == messageHash) && (Math.Abs((now - previousUserActivityInGuild.InsertDate).TotalSeconds) < 60) ? 0 : 1;
        // Time-based factor
        // Use a smoothstep curve over 5s: s in [0,1], timeXp = s^2 * (3 - 2s), harsher near rapid sends.
        double speedPenaltySimple = 1.0;
        if (previousUserActivityInGuild != null)
        {
            double s = (now - previousUserActivityInGuild.InsertDate).TotalMilliseconds / 5000.0;
            if (s < 0) s = 0; else if (s > 1) s = 1;
            speedPenaltySimple = s * s * (3 - 2 * s);
        }

        // Similarity penalty via SimHash against last 10 messages (ignore very short normalized texts)
        double similarityPenaltyComplex = 1.0;
        if (normLen >= 12 && lastTen.Count > 0 && simHash != 0UL)
        {
            double maxSimilarity = 0.0;
            DateTime newestTs = previousUserActivityInGuild?.InsertDate ?? DateTime.MinValue;
            foreach (var prev in lastTen)
            {
                if (prev.MessageSimHash == 0UL || prev.NormalizedLength < 12)
                    continue;
                int hd = SimHasher.HammingDistance(simHash, prev.MessageSimHash);
                double sim = 1.0 - (hd / 64.0);
                if (sim > maxSimilarity)
                    maxSimilarity = sim;
            }

            // Apply thresholded penalty (tuneable)
            if (maxSimilarity >= 0.92)
                similarityPenaltyComplex = 0.0; // effectively no XP for near-duplicates
            else if (maxSimilarity >= 0.85)
                similarityPenaltyComplex = 0.25; // heavy penalty
        }

        // Typing speed penalty based on WPM estimated from time since previous user activity
        // After 200 WPM start penalizing logarithmically until 300 WPM where XP becomes 0
        double speedPenaltyComplex = 1.0;
        if (previousUserActivityInGuild != null && message.Content.Length >= 50)
        {
            double minutesSincePrev = Math.Max((now - previousUserActivityInGuild.InsertDate).TotalMinutes, 1e-6); // avoid div-by-zero
            double charsTyped = message.Content.Length;
            double cpm = charsTyped / minutesSincePrev; // characters per minute
            double wpm = cpm / 5.0; // 5 chars ≈ 1 word

            if (wpm > 200)
            {
                if (wpm >= 300)
                {
                    speedPenaltyComplex = 0.0;
                }
                else
                {
                    // Map wpm in (200,300) to x in (0,1) and use a logarithmic drop
                    double x = (wpm - 200.0) / 100.0; // 0..1
                    // dec goes 0->1 using log base 10: ln(1+9x)/ln(10)
                    double dec = Math.Log(1.0 + 9.0 * x) / Math.Log(10.0);
                    speedPenaltyComplex = 1.0 - dec; // 1->0
                }
            }
        }

        // Final XP: length and hash/time factors; timeXp applies to short messages, speedPenalty to fast typing, simPenalty for similarity
        int xp = (int)Math.Floor((baseXP + messageLengthXp) * similarityPenaltySimple * similarityPenaltyComplex * speedPenaltySimple * speedPenaltyComplex);

        // Calculate average message length and message count
        double averageMessageLength = message.Content.Length;
        int messageCount = 1;

        if (previousActivityInGuild != null)
        {
            messageCount = previousActivityInGuild.GuildMessageCount + 1;
            averageMessageLength = ((previousActivityInGuild.GuildAverageMessageLength * previousActivityInGuild.GuildMessageCount) + message.Content.Length) / messageCount;
        }

        // Create new user activity record
        UserActivity userActivity = new()
        {
            DiscordChannelId = message.Channel.Id,
            GuildId = guild.Id,
            InsertDate = DateTime.UtcNow,
            MessageHash = messageHash,
            UserId = user.Id,
            XpGained = xp,
            MessageLength = message.Content.Length,
            MessageSimHash = simHash,
            NormalizedLength = normLen,
            GuildAverageMessageLength = averageMessageLength,
            GuildMessageCount = messageCount
        };

        dbContext.Add(userActivity);

        // Update user's XP and Level in UserLevels
        UserLevels? userLevel = dbContext.UserLevels
            .FirstOrDefault(ul => ul.UserId == user.Id && ul.GuildId == guild.Id);

        bool newUserLevel = false;
        var postSaveActions = new List<Func<Task>>();

        if (userLevel == null)
            newUserLevel = true;

        userLevel ??= new UserLevels
        {
            UserId = user.Id,
            GuildId = guild.Id
        };

        userLevel.TotalXp += xp;

        int newLevel = CalculateLevel(userLevel.TotalXp);

        if (userLevel.Level != newLevel)
        {
            userLevel.Level = newLevel;

            // Prepare level up message and optionally pre-select a random quote (efficient DB-side selection)
            int levelToAnnounce = newLevel;
            string? selectedQuoteContent = null;

            if (guild.LevelUpQuotes)
            {
                Quote? pick = null;
                if (guild.UseGlobalQuotes)
                {
                    pick = await dbContext.Quotes
                        .Where(q => q.Approved && !q.Removed)
                        .OrderBy(q => EF.Functions.Random())
                        .FirstOrDefaultAsync();
                }
                else
                {
                    pick = await dbContext.Quotes
                        .Where(q => q.Approved && !q.Removed && q.GuildId == guild.Id)
                        .OrderBy(q => EF.Functions.Random())
                        .FirstOrDefaultAsync();
                }

                if (pick != null)
                    selectedQuoteContent = pick.Content;
            }

            postSaveActions.Add(async () =>
            {
                try
                {
                    var noPing = new AllowedMentions(AllowedMentionTypes.None);

                    // Discord guild and channel context
                    var discordGuild = guildChannel.Guild;

                    // LEVEL MESSAGE
                    if (guild.LevelUpMessages)
                    {
                        // Choose target channel: configured channel id or current channel
                        SocketTextChannel? target = null;
                        if (guild.LevelUpMessagesChannelId != 0)
                        {
                            target = discordGuild.GetTextChannel(guild.LevelUpMessagesChannelId);
                        }

                        if (target == null)
                        {
                            // fallback to current channel if it's a text channel
                            target = message.Channel as SocketTextChannel;
                        }

                        if (target != null)
                        {
                            // Send the mention token but prevent an actual ping by clearing allowed mentions
                            await target.SendMessageAsync($"{message.Author.Mention} leveled up to level {levelToAnnounce}! {happyEmojisBag.Random()}", allowedMentions: noPing);
                        }
                    }

                    // QUOTE MESSAGE (send pre-selected content if any)
                    if (!string.IsNullOrEmpty(selectedQuoteContent))
                    {
                        SocketTextChannel? quoteTarget = null;

                        if (guild.LevelUpQuotesChannelId != 0)
                        {
                            quoteTarget = discordGuild.GetTextChannel(guild.LevelUpQuotesChannelId);
                        }

                        if (quoteTarget == null)
                        {
                            quoteTarget = message.Channel as SocketTextChannel;
                        }

                        if (quoteTarget != null)
                        {
                            // If we're posting the quote to the same channel that triggered the level up,
                            // send it as a reply to the triggering message for clearer context.
                            if (quoteTarget.Id == message.Channel.Id && message is SocketUserMessage)
                            {
                                ulong? guildId = null;
                                if (message.Channel is SocketGuildChannel sgc)
                                    guildId = sgc.Guild.Id;

                                var reference = new MessageReference(message.Id, message.Channel.Id, guildId);
                                await quoteTarget.SendMessageAsync(text: selectedQuoteContent, messageReference: reference, allowedMentions: noPing);
                            }
                            else
                            {
                                await quoteTarget.SendMessageAsync(selectedQuoteContent, allowedMentions: noPing);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending level up message: {ex}");
                    // don't let messaging errors bubble up and break activity handling
                }
            });
        }

        if (newUserLevel)
            dbContext.Add(userLevel);

        await dbContext.SaveChangesAsync();

        // Execute any post-save actions (send level up messages and quotes)
        foreach (var action in postSaveActions)
        {
            _ = Task.Run(action);
        }
    }

    public static int CalculateLevel(long xp)
    {
        // (* 10 because on average users get 10 XP per message) 
        // CalculateLevel(100 * 10) = ~1
        // CalculateLevel(100000 * 10) = ~1000

        return (int)Math.Pow(Math.Log10((xp + 111) / 111), 5.0243);
    }

    public static int CalculateXp(int level)
    {
        return (int)(111 * Math.Pow(10, Math.Pow(level, 1.0 / 5.0243)) - 111);
    }
}
