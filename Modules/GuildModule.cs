using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Extensions;
using Morpheus.Handlers;

namespace Morpheus.Modules;

public class GuildModule : ModuleBase<SocketCommandContextExtended>
{
    private readonly CommandService commands;
    private readonly IServiceProvider serviceProvider;
    private readonly DB dbContext;
    private readonly int HelpPageSize = 10;

    public GuildModule(DiscordSocketClient client, CommandService commands, InteractionsHandler interactionHandler, IServiceProvider serviceProvider, DB dbContext)
    {
        this.commands = commands;
        this.serviceProvider = serviceProvider;
        this.dbContext = dbContext;
    }


    [Name("Set Welcome Channel")]
    [Summary("Sets the welcome channel where new join messages will appear.")]
    [Command("setwchannel")]
    [Alias("setwc", "swc", "welcomechannel")]
    [RequireUserPermission(Discord.GuildPermission.Administrator)]
    [RateLimit(1, 10)]
    public async Task SetWelcomeChanelAsync([Remainder] SocketChannel? channel = null)
    {
        var guild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == Context.Guild.Id);

        if (guild == null)
        {
            await ReplyAsync("Your guild hasn't been added to the database yet, please try again.");
            return;
        }

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
    public async Task SetCommandsPrefix([Remainder] string prefix = "m!")
    {
        var guild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == Context.Guild.Id);

        if (guild == null)
        {
            await ReplyAsync("Your guild hasn't been added to the database yet, please try again.");
            return;
        }

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
    public async Task SetPinsChannelAsync([Remainder] SocketChannel? channel = null)
    {
        var guild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == Context.Guild.Id);
        if (guild == null)
        {
            await ReplyAsync("Your guild hasn't been added to the database yet, please try again.");
            return;
        }

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
