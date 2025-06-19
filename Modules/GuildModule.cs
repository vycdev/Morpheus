using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Extensions;
using Morpheus.Handlers;

namespace Morpheus.Modules;

public class GuildModule(DiscordSocketClient client, CommandService commands, InteractionsHandler interactionHandler, IServiceProvider serviceProvider, DB dbContext) : ModuleBase<SocketCommandContextExtended>
{
    private readonly CommandService commands = commands;
    private readonly IServiceProvider serviceProvider = serviceProvider;
    private readonly DB dbContext = dbContext;
    private readonly int HelpPageSize = 10;

    [Name("Set Welcome Channel")]
    [Summary("Sets the welcome channel where new join messages will appear.")]
    [Command("setwchannel")]
    [Alias("setwc", "swc", "welcomechannel")]
    [RequireUserPermission(Discord.GuildPermission.Administrator)]
    [RateLimit(1, 10)]
    [RequireDbGuild]
    public async Task SetWelcomeChanelAsync([Remainder] SocketChannel? channel = null)
    {
        Database.Models.Guild guild = Context.DbGuild!;

        guild.WelcomeChannelId = channel?.Id ?? 0;

        await dbContext.SaveChangesAsync();

        if (guild.WelcomeChannelId == 0)
        {
            await ReplyAsync("Welcome channel has been removed.");
            return;
        }

        await ReplyAsync($"Welcome channel has been set. Your channel is <#{guild.WelcomeChannelId}>");
    }

    [Name("Set Commands Prefix")]
    [Summary("Sets the welcome channel where new join messages will appear.")]
    [Command("setprefix")]
    [Alias("setcommandsprefix", "setcp")]
    [RequireUserPermission(Discord.GuildPermission.Administrator)]
    [RateLimit(1, 10)]
    [RequireDbGuild]
    public async Task SetCommandsPrefix([Remainder] string prefix = "m!")
    {
        Database.Models.Guild guild = Context.DbGuild!;

        if (string.IsNullOrWhiteSpace(prefix) || prefix.Length > 3)
        {
            await ReplyAsync($"The prefix you picked, `{prefix}`, is not valid. Make sure that the prefix is not empty or is longer than 3 characters.");
            return;
        }

        guild.Prefix = prefix;
        await dbContext.SaveChangesAsync();

        if (prefix == "m!")
        {
            await ReplyAsync("Prefix has been reset back to `m!`.");
            return;
        }

        await ReplyAsync($"Prefix has been set. Your prefix is `{prefix}`");
    }

    [Name("Set Pins Channel")]
    [Summary("Sets the pins channel where pinned messages will appear.")]
    [Command("setpinschannel")]
    [Alias("setpc", "spc", "pinschannel")]
    [RequireUserPermission(Discord.GuildPermission.Administrator)]
    [RateLimit(1, 10)]
    [RequireDbGuild]
    public async Task SetPinsChannelAsync([Remainder] SocketChannel? channel = null)
    {
        Database.Models.Guild guild = Context.DbGuild!;

        guild.PinsChannelId = channel?.Id ?? 0;
        await dbContext.SaveChangesAsync();

        // Confirmation reply
        if (guild.PinsChannelId == 0)
        {
            await ReplyAsync("Pins channel has been removed.");
            return;
        }

        await ReplyAsync($"Pins channel has been set. Your channel is <#{guild.PinsChannelId}>");
    }
}
