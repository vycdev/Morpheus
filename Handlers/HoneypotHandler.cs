using Discord;
using Discord.WebSocket;
using Morpheus.Database.Models;
using Morpheus.Database;
using Morpheus.Services;
using Morpheus.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Morpheus.Utilities.Lists;

namespace Morpheus.Handlers;

public class HoneypotHandler
{
    private readonly DiscordSocketClient client;
    private readonly IServiceScopeFactory scopeFactory;
    private static bool started = false;

    public HoneypotHandler(DiscordSocketClient client, IServiceScopeFactory scopeFactory)
    {
        if (started)
            throw new InvalidOperationException("At most one instance of this service can be started");

        started = true;

        this.client = client;
        this.scopeFactory = scopeFactory;

        client.MessageReceived += HandleMessageReceived;
    }

    private async Task HandleMessageReceived(SocketMessage messageParam)
    {
        // Don't process system messages or bot messages
        if (messageParam is not SocketUserMessage message)
            return;

        // Don't ban bots
        if (message.Author.IsBot)
            return;

        // Only process messages in guild channels
        if (message.Channel is not SocketGuildChannel guildChannel)
            return;

        using var scope = scopeFactory.CreateScope();
        var guildService = scope.ServiceProvider.GetRequiredService<GuildService>();
        var db = scope.ServiceProvider.GetRequiredService<DB>();

        Guild? guild = await guildService.TryGetCreateGuild(guildChannel.Guild);

        if (guild == null)
            return;

        // Check if honeypot is enabled and welcome channel is set
        if (!guild.SendHoneypotMessages || guild.HoneypotChannelId == 0 || guild.WelcomeChannelId == 0)
            return;

        // Check if the message is in the honeypot channel
        if (message.Channel.Id != guild.HoneypotChannelId)
            return;

        // Get the honeypot channel as SocketTextChannel for logging
        if (message.Channel is not SocketTextChannel honeypotChannel)
            return;

        // Get the welcome channel for sending the notification
        SocketTextChannel? welcomeChannel = guildChannel.Guild.GetTextChannel(guild.WelcomeChannelId);
        if (welcomeChannel == null)
            return;

        // Get the user as a guild member
        SocketGuildUser? guildUser = guildChannel.Guild.GetUser(message.Author.Id);
        if (guildUser == null)
            return;

        // Don't ban administrators
        if (guildUser.GuildPermissions.Administrator)
            return;

        // Ban the user and delete their messages from the past day
        try
        {
            // Ban with pruneDays set to 1 (deletes messages from past 24 hours)
            await guildChannel.Guild.AddBanAsync(
                user: guildUser,
                pruneDays: 1,
                reason: $"Honeypot triggered (7 day ban) on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"
            );

            // Send notification to welcome channel
            await welcomeChannel.SendMessageAsync(
                $"{guildUser.Mention} has been automatically banned after posting in the honeypot channel."
            );

            // Record temporary ban for 7 days
            db.TemporaryBans.Add(new TemporaryBan
            {
                GuildId = guildChannel.Guild.Id,
                UserId = guildUser.Id,
                Reason = $"Honeypot temporary ban. Banned on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            var logsService = scope.ServiceProvider.GetRequiredService<LogsService>();
            logsService.Log($"HoneypotHandler: failed to ban user {message.Author.Mention}: {ex.Message}");
        }
    }
}
