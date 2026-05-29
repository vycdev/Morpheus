using Discord.Commands;
using Discord;
using Discord.WebSocket;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;
using Microsoft.EntityFrameworkCore;
using Morpheus.Utilities.Lists;
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
        var activityScoringService = scope.ServiceProvider.GetRequiredService<ActivityScoringService>();
        var activityLevelService = scope.ServiceProvider.GetRequiredService<ActivityLevelService>();

        User user = await usersService.TryGetCreateUser(message.Author);
        await usersService.TryUpdateUsername(message.Author, user);

        // If the guild doesn't exist, create it and then get it
        Guild guild = await guildService.TryGetCreateGuild(guildChannel.Guild);

        DateTime now = DateTime.UtcNow;
        UserActivity userActivity = await activityScoringService.CreateActivityAsync(
            user.Id,
            guild.Id,
            message.Channel.Id,
            message.Id,
            message.Content,
            now);

        var postSaveActions = new List<Func<Task>>();

        ActivityLevelUpdateResult levelUpdate = await activityLevelService.RecordActivityAsync(userActivity);

        if (levelUpdate.LevelChanged)
        {
            // Prepare level up message and optionally pre-select a random quote (efficient DB-side selection)
            int levelToAnnounce = levelUpdate.NewLevel;
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

        return ActivityLevelService.CalculateLevel(xp);
    }

    public static int CalculateXp(int level)
    {
        return (int)(111 * Math.Pow(10, Math.Pow(level, 1.0 / 5.0243)) - 111);
    }
}
