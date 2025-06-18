using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Services;
using Morpheus.Utilities;
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
    private bool started = false;

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
        var message = messageParam as SocketUserMessage;
        if (message == null)
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
        double messageLengthXp = message.Content.Length / previousActivityInGuild?.GuildAverageMessageLength ?? 1; 
        // If the message hash is the same as the previous message and sent within 30 seconds, no XP is gained
        int messageHashXp = (previousActivity?.MessageHash == messageHash) && (Math.Abs((now - previousActivity.InsertDate).TotalSeconds) < 30) ? 0 : 1;
        // Scale XP based on time since the last message, with a maximum of 10 seconds (spamming messages gives diminishing returns)
        double timeXp = previousActivity != null ? Math.Min(Math.Abs((now - previousActivity.InsertDate).TotalSeconds), 10) / 10 : 1;

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

        userLevel ??= new UserLevels
        {
            UserId = user.Id,
            GuildId = guild.Id
        };

        userLevel.TotalXp += xp;

        if(userLevel.Level != CalculateLevel(userLevel.TotalXp))
        {
            // Level up message / quote 
        }

        userLevel.Level = CalculateLevel(userLevel.TotalXp);
        dbContext.SaveChanges();
    }

    public static int CalculateLevel(int xp)
    {
        return (int)(Math.Pow(xp / 1000, 1.5));
    }

    public static int CalculateXp(int level)
    {
        return (int)(1000 * Math.Pow(level, 2.0 / 3.0));
    }
}
