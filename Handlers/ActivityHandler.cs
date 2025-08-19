using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Services;
using Morpheus.Utilities;
using Morpheus.Utilities.Lists;
using System;
using System.Collections.Generic;
using System.IO.Hashing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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
        UserActivity? previousActivity = dbContext.UserActivity
            .Where(ua => ua.UserId == user.Id && ua.GuildId == guild.Id)
            .OrderByDescending(ua => ua.InsertDate)
            .FirstOrDefault();

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
        int messageHashXp = (previousActivity?.MessageHash == messageHash) && (Math.Abs((now - previousActivity.InsertDate).TotalSeconds) < 30) ? 0 : 1;
        // Scale XP based on time since the last message, with a maximum of 5 seconds (spamming messages gives diminishing returns)
        double timeXp = previousActivity != null ? Math.Min(Math.Abs((now - previousActivity.InsertDate).TotalMilliseconds), 5 * 1000) / (5 * 1000) : 1;

        int xp = (int)Math.Floor(baseXP + messageLengthXp * messageHashXp * timeXp);

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

            // Prepare level up message and optional quote to be sent after successful save
            int levelToAnnounce = newLevel;
            postSaveActions.Add(async () =>
            {
                try
                {
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
                            await target.SendMessageAsync($"{message.Author.Mention} leveled up to level {levelToAnnounce}! {happyEmojisBag.Random()}");
                        }
                    }

                    // QUOTE MESSAGE
                    if (guild.LevelUpQuotes)
                    {
                        // build quote query: approved && !removed
                        List<Quote> quotes;
                        if (guild.UseGlobalQuotes)
                        {
                            quotes = dbContext.Quotes.Where(q => q.Approved && !q.Removed).ToList();
                        }
                        else
                        {
                            quotes = dbContext.Quotes.Where(q => q.Approved && !q.Removed && q.GuildId == guild.Id).ToList();
                        }

                        if (quotes != null && quotes.Count > 0)
                        {
                            var random = new Random();
                            var pick = quotes[random.Next(quotes.Count)];
                            string content = pick.Content ?? string.Empty;

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
                                // send raw content only
                                await quoteTarget.SendMessageAsync(content);
                            }
                        }
                    }
                }
                catch
                {
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
