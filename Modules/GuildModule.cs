using Discord.Commands;
using Discord.WebSocket;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Extensions;
using Morpheus.Handlers;
using Discord;
using Morpheus.Utilities;
using Morpheus.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace Morpheus.Modules;

public class GuildModule(DiscordSocketClient client, CommandService commands, InteractionsHandler interactionHandler, IServiceProvider serviceProvider, DB dbContext) : ModuleBase<SocketCommandContextExtended>
{
    private readonly CommandService commands = commands;
    private readonly DiscordSocketClient client = client;
    private readonly InteractionsHandler interactionHandler = interactionHandler;
    private readonly IServiceProvider serviceProvider = serviceProvider;
    private readonly DB dbContext = dbContext;
    private readonly int HelpPageSize = 10;

    [Name("Set Welcome Channel")]
    [Summary("Sets the welcome channel where new join messages will appear.")]
    [Command("setwchannel")]
    [Alias("setwc", "swc", "welcomechannel")]
    [RequireUserPermission(Discord.GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(1, 10)]
    public async Task SetWelcomeChanelAsync([Remainder] SocketChannel? channel = null)
    {
        // Load tracked Guild entity from DB instead of using Context.DbGuild (which may be detached)
        var guild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == Context.Guild.Id);
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
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
    [RequireContext(ContextType.Guild)]
    [RateLimit(1, 10)]
    public async Task SetCommandsPrefix([Remainder] string prefix = "m!")
    {
        var guild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == Context.Guild.Id);
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        if (string.IsNullOrWhiteSpace(prefix) || prefix.Length > 3)
        {
            await ReplyAsync($"The prefix you picked, `{prefix}`, is not valid. Make sure that the prefix is not empty and at most 3 characters.");
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
    [RequireContext(ContextType.Guild)]
    [RateLimit(1, 10)]
    public async Task SetPinsChannelAsync([Remainder] SocketChannel? channel = null)
    {
        var guild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == Context.Guild.Id);
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
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

    [Name("Set Level Up Messages Channel")]
    [Summary("Sets or removes the channel where level up messages will be posted.")]
    [Command("setlevelupmessageschannel")]
    [Alias("setlumchannel", "setlevelupmsgschannel")]
    [RequireUserPermission(Discord.GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(1, 10)]
    public async Task SetLevelUpMessagesChannelAsync([Remainder] SocketChannel? channel = null)
    {
        var guild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == Context.Guild.Id);
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        guild.LevelUpMessagesChannelId = channel?.Id ?? 0;
        await dbContext.SaveChangesAsync();

        if (guild.LevelUpMessagesChannelId == 0)
        {
            await ReplyAsync("Level up messages channel has been removed.");
            return;
        }

        await ReplyAsync($"Level up messages channel set to <#{guild.LevelUpMessagesChannelId}>.");
    }

    [Name("Set Level Up Quotes Channel")]
    [Summary("Sets or removes the channel where level up quotes will be posted.")]
    [Command("setlevelupquoteschannel")]
    [Alias("setluqchannel", "setlevelupquoteschan")]
    [RequireUserPermission(Discord.GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(1, 10)]
    public async Task SetLevelUpQuotesChannelAsync([Remainder] SocketChannel? channel = null)
    {
        var guild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == Context.Guild.Id);
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        guild.LevelUpQuotesChannelId = channel?.Id ?? 0;
        await dbContext.SaveChangesAsync();

        if (guild.LevelUpQuotesChannelId == 0)
        {
            await ReplyAsync("Level up quotes channel has been removed.");
            return;
        }

        await ReplyAsync($"Level up quotes channel set to <#{guild.LevelUpQuotesChannelId}>.");
    }

    [Name("Toggle Level Up Messages")]
    [Summary("Toggles whether level up messages are posted in this guild.")]
    [Command("togglevlevelupmsgs")]
    [Alias("togglevmsgs", "togglelevelupmessages")]
    [RequireUserPermission(Discord.GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(1, 10)]
    public async Task ToggleLevelUpMessages()
    {
        var guild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == Context.Guild.Id);
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        guild.LevelUpMessages = !guild.LevelUpMessages;
        await dbContext.SaveChangesAsync();
        await ReplyAsync($"Level up messages are now {(guild.LevelUpMessages ? "enabled" : "disabled")}.");
    }

    [Name("Toggle Level Up Quotes")]
    [Summary("Toggles whether level up quotes are posted in this guild.")]
    [Command("togglelevelupquotes")]
    [Alias("togglevquotes")]
    [RequireUserPermission(Discord.GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(1, 10)]
    public async Task ToggleLevelUpQuotes()
    {
        var guild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == Context.Guild.Id);
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        guild.LevelUpQuotes = !guild.LevelUpQuotes;
        await dbContext.SaveChangesAsync();
        await ReplyAsync($"Level up quotes are now {(guild.LevelUpQuotes ? "enabled" : "disabled")}.");
    }

    [Name("Toggle Use Global Quotes")]
    [Summary("Toggles whether this guild uses global quotes instead of guild-only quotes.")]
    [Command("toggleglobalquotes")]
    [Alias("useglobalquotes", "toggleuseglobalquotes")]
    [RequireUserPermission(Discord.GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(1, 10)]
    public async Task ToggleUseGlobalQuotes()
    {
        var guild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == Context.Guild.Id);
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        guild.UseGlobalQuotes = !guild.UseGlobalQuotes;
        await dbContext.SaveChangesAsync();
        if (guild.UseGlobalQuotes)
        {
            await ReplyAsync("Use global quotes is now enabled. " +
                "Warning: enabling global quotes means quotes from other guilds may contain NSFW, offensive, or otherwise unwanted content. " +
                "Be cautious and consider disabling this option if you encounter problematic quotes.");
        }
        else
        {
            await ReplyAsync("Use global quotes is now disabled.");
        }
    }

    [Name("Set Quotes Approval Channel")]
    [Summary("Sets or removes the channel where quote approvals will be posted.")]
    [Command("setquotesapprovalchannel")]
    [Alias("setqapproval", "setquoteschannel")]
    [RequireUserPermission(Discord.GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(1, 10)]
    public async Task SetQuotesApprovalChannel([Remainder] SocketChannel? channel = null)
    {
        var guild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == Context.Guild.Id);
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        guild.QuotesApprovalChannelId = channel?.Id ?? 0;
        await dbContext.SaveChangesAsync();

        if (guild.QuotesApprovalChannelId == 0)
        {
            await ReplyAsync("Quotes approval channel has been removed.");
            return;
        }

        await ReplyAsync($"Quotes approval channel set to <#{guild.QuotesApprovalChannelId}>");
    }

    [Name("Set Quote Add Required Approvals")]
    [Summary("Sets how many approvals are required to add a quote.")]
    [Command("setquoteaddapprovals")]
    [Alias("setquoteadd", "setaddapprovals")]
    [RequireUserPermission(Discord.GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(1, 10)]
    public async Task SetQuoteAddRequiredApprovals(int approvals)
    {
        if (approvals < 1)
        {
            await ReplyAsync("Approvals must be at least 1.");
            return;
        }

        if (approvals > 1000)
        {
            await ReplyAsync("Approvals must be at most 1000.");
            return;
        }

        var guild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == Context.Guild.Id);
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        guild.QuoteAddRequiredApprovals = approvals;
        await dbContext.SaveChangesAsync();
        await ReplyAsync($"Quote add required approvals set to {approvals}.");
    }

    [Name("Set Quote Remove Required Approvals")]
    [Summary("Sets how many approvals are required to remove a quote.")]
    [Command("setquoteremoveapprovals")]
    [Alias("setquoteremove", "setremoveapprovals")]
    [RequireUserPermission(Discord.GuildPermission.Administrator)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(1, 10)]
    public async Task SetQuoteRemoveRequiredApprovals(int approvals)
    {
        if (approvals < 1)
        {
            await ReplyAsync("Approvals must be at least 1.");
            return;
        }

        if (approvals > 1000)
        {
            await ReplyAsync("Approvals must be at most 1000.");
            return;
        }

        var guild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == Context.Guild.Id);
        if (guild == null)
        {
            await ReplyAsync("Guild not found.");
            return;
        }

        guild.QuoteRemoveRequiredApprovals = approvals;
        await dbContext.SaveChangesAsync();
        await ReplyAsync($"Quote remove required approvals set to {approvals}.");
    }

    [Name("Guild Info")]
    [Summary("Displays information about the current guild.")]
    [Command("guildinfo")]
    [Alias("serverinfo", "guild", "server")]
    [RateLimit(3, 10)]
    [RequireContext(ContextType.Guild)]
    public async Task GuildInfo()
    {
        SocketGuild guild = Context.Guild;
        // Database-backed guild settings
        Guild dbGuild = Context.DbGuild!;

        string ChannelOrNone(ulong id) => id == 0 ? "Not set" : $"<#{id}>";
        string BoolStatus(bool v) => v ? "Enabled" : "Disabled";

        string settings =
            $"Prefix: {dbGuild.Prefix}\n" +
            $"Welcome Channel: {ChannelOrNone(dbGuild.WelcomeChannelId)}\n" +
            $"Pins Channel: {ChannelOrNone(dbGuild.PinsChannelId)}\n" +
            $"Level Up Messages Channel: {ChannelOrNone(dbGuild.LevelUpMessagesChannelId)}\n" +
            $"Level Up Quotes Channel: {ChannelOrNone(dbGuild.LevelUpQuotesChannelId)}\n" +
            $"Level Up Messages: {BoolStatus(dbGuild.LevelUpMessages)}\n" +
            $"Level Up Quotes: {BoolStatus(dbGuild.LevelUpQuotes)}\n" +
            $"Use Global Quotes: {BoolStatus(dbGuild.UseGlobalQuotes)}\n" +
            $"Quotes Approval Channel: {ChannelOrNone(dbGuild.QuotesApprovalChannelId)}\n" +
            $"Quote Add Required Approvals: {dbGuild.QuoteAddRequiredApprovals}\n" +
            $"Quote Remove Required Approvals: {dbGuild.QuoteRemoveRequiredApprovals}\n" +
            $"Welcome Messages: {BoolStatus(dbGuild.WelcomeMessages)}\n" +
            $"Use Activity Roles: {BoolStatus(dbGuild.UseActivityRoles)}\n" +
            $"Bot Join Date: {dbGuild.InsertDate.ToString("yyyy-MM-dd HH:mm 'UTC'")}";

        EmbedBuilder embed = new()
        {
            Color = Colors.Blue,
            Title = $"Guild Info: {guild.Name}",
            ThumbnailUrl = guild.IconUrl,
            Description = $"ID: {guild.Id}\nOwner: {guild.Owner.Username}#{guild.Owner.Discriminator}\nCreated At: {guild.CreatedAt.UtcDateTime}",
            Fields =
            {
                new EmbedFieldBuilder().WithName("Member Count").WithValue(guild.MemberCount),
                new EmbedFieldBuilder().WithName("Verification Level").WithValue(guild.VerificationLevel.ToString()),
                new EmbedFieldBuilder().WithName("Roles").WithValue(guild.Roles.Count),
                new EmbedFieldBuilder().WithName("Channels").WithValue(guild.Channels.Count),
                new EmbedFieldBuilder().WithName("Settings").WithValue(settings).WithIsInline(false)
            },
            Footer = new EmbedFooterBuilder()
            {
                Text = "Guild Info",
                IconUrl = guild.IconUrl
            }
        };

        await ReplyAsync(embed: embed.Build());
    }
}
