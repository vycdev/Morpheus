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
using Morpheus.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Morpheus.Handlers;

public class ActivityHandler
{
    private readonly DiscordSocketClient client;
    private readonly CommandService commands;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly RandomBag happyEmojisBag = new(EmojiList.EmojisHappy);
    private static bool started = false;

    public ActivityHandler(DiscordSocketClient client, CommandService commands, IServiceScopeFactory scopeFactory)
    {
        if (started)
            throw new InvalidOperationException("At most one instance of this service can be started");

        started = true;

        this.client = client;
        this.commands = commands;
        this.scopeFactory = scopeFactory;

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

        using IServiceScope scope = scopeFactory.CreateScope();
        var usersService = scope.ServiceProvider.GetRequiredService<UsersService>();
        var guildService = scope.ServiceProvider.GetRequiredService<GuildService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DB>();

        User user = await usersService.TryGetCreateUser(message.Author);
        await usersService.TryUpdateUsername(message.Author, user);

        // If the guild doesn't exist, create it and then get it
        Guild guild = await guildService.TryGetCreateGuild(guildChannel.Guild);

        // Timestamp and config
        DateTime now = DateTime.UtcNow;
        int similarityWindowMinutes = Env.Get<int>("ACTIVITY_SIMILARITY_WINDOW_MINUTES", 10);
        DateTime similarityWindowStart = now.AddMinutes(-similarityWindowMinutes);

        string messageHash = Convert.ToBase64String(XxHash64.Hash(Encoding.UTF8.GetBytes(message.Content)));
        // Compute SimHash over normalized trigrams (non-reversible)
        (ulong simHash, int normLen) = SimHasher.ComputeSimHash(message.Content);

        UserActivity? previousUserActivityInGuild = dbContext.UserActivity
                .Where(ua => ua.UserId == user.Id && ua.GuildId == guild.Id)
                .OrderByDescending(ua => ua.InsertDate)
                .FirstOrDefault();

        // Fetch recent user activities for similarity checks within time window (cap for safety)
        var recentForSimilarity = dbContext.UserActivity
                .Where(ua => ua.UserId == user.Id && ua.GuildId == guild.Id && ua.InsertDate >= similarityWindowStart)
                .OrderByDescending(ua => ua.InsertDate)
                .Select(ua => new { ua.MessageSimHash, ua.NormalizedLength, ua.InsertDate })
                .Take(200)
                .ToList();

        UserActivity? previousActivityInGuild = dbContext.UserActivity
                .Where(ua => ua.GuildId == guild.Id)
                .OrderByDescending(ua => ua.InsertDate)
                .FirstOrDefault();

        // Base XP for sending a message
        int baseXP = 1;
        // Length-based XP with logarithmic taper relative to guild average
        // r = L / A, clamped to [0, 100]; bonus = B * log(1 + k*r) / log(1 + k)
        // With B=4, k=0.1 -> at r=1 (about average length), bonus≈4
        double avgLen = previousActivityInGuild?.GuildAverageMessageLength ?? 0.0;
        double r = 1.0;
        if (avgLen > 0.0)
            r = message.Content.Length / avgLen;
        if (r < 0.0) r = 0.0; else if (r > 100.0) r = 100.0;
        const double B_len = 4.0;
        const double k_len = 0.025;
        double denom_len = Math.Log(1.0 + k_len);
        double messageLengthXp = denom_len > 0.0
            ? B_len * Math.Log(1.0 + (k_len * r)) / denom_len
            : B_len * r; // extremely unlikely fallback

        // If the message hash is the same as the previous message and sent within 60 seconds, no XP is gained
        int similarityPenaltySimple = (previousUserActivityInGuild?.MessageHash == messageHash) && (Math.Abs((now - previousUserActivityInGuild.InsertDate).TotalSeconds) < 60) ? 0 : 1;

        // Time-based factor: logarithmic rise over 0..5 seconds
        // 0s => 0 (100% penalty), ~2.5s => >0.5 (most penalty gone), 5s => ~1 (almost no penalty)
        double speedPenaltySimple = 1.0;
        if (previousUserActivityInGuild != null)
        {
            double dtSec = (now - previousUserActivityInGuild.InsertDate).TotalSeconds;
            if (dtSec < 0) dtSec = 0; else if (dtSec > 5) dtSec = 5;
            const double k = 9.0; // curvature
            speedPenaltySimple = Math.Log(1.0 + k * dtSec) / Math.Log(1.0 + k * 5.0);
        }

        // Similarity penalty via SimHash against recent messages in configured time window (ignore very short normalized texts)
        double similarityPenaltyComplex = 1.0;
        if (normLen >= 12 && recentForSimilarity.Count > 0 && simHash != 0UL)
        {
            double maxSimilarity = 0.0;
            foreach (var prev in recentForSimilarity)
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

        // Calculate EMA average message length and message count
        const double emaAlpha = 2.0 / (500.0 + 1.0); // N=500
        double averageMessageLength;
        int messageCount;
        if (previousActivityInGuild == null)
        {
            messageCount = 1;
            averageMessageLength = message.Content.Length;
        }
        else
        {
            messageCount = previousActivityInGuild.GuildMessageCount + 1;
            double prevAvg = previousActivityInGuild.GuildAverageMessageLength;
            if (prevAvg <= 0.0)
                averageMessageLength = message.Content.Length;
            else
                averageMessageLength = (1.0 - emaAlpha) * prevAvg + emaAlpha * message.Content.Length;
        }

        // Create new user activity record
        UserActivity userActivity = new()
        {
            DiscordChannelId = message.Channel.Id,
            DiscordMessageId = message.Id,
            GuildId = guild.Id,
            InsertDate = now,
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

        // Update per-user message length stats (raw characters)
        const double userEmaAlpha = 2.0 / (500.0 + 1.0); // N=500
        int prevUserMsgCount = userLevel.UserMessageCount;
        double prevUserAvgLen = userLevel.UserAverageMessageLength;
        double prevUserEmaLen = userLevel.UserAverageMessageLengthEma;

        int newUserMsgCount = prevUserMsgCount + 1;
        double msgLen = message.Content.Length;
        double newUserAvgLen = prevUserMsgCount > 0
            ? ((prevUserAvgLen * prevUserMsgCount) + msgLen) / newUserMsgCount
            : msgLen;
        double newUserEmaLen = prevUserEmaLen <= 0.0
            ? msgLen
            : (1.0 - userEmaAlpha) * prevUserEmaLen + userEmaAlpha * msgLen;

        userLevel.UserMessageCount = newUserMsgCount;
        userLevel.UserAverageMessageLength = newUserAvgLen;
        userLevel.UserAverageMessageLengthEma = newUserEmaLen;

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
