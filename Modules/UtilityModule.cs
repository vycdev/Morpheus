using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Handlers;
using Morpheus.Utilities;
using System.IO.Compression;

namespace Morpheus.Modules;

public class UtilityModule : ModuleBase<SocketCommandContextExtended>
{
    private static readonly HttpClient httpClient = new();

    private readonly CommandService commands;
    private readonly IServiceProvider serviceProvider;
    private readonly DB dbContext;

    public UtilityModule(DiscordSocketClient client, CommandService commands, InteractionsHandler interactionHandler, IServiceProvider serviceProvider, DB dbContext)
    {
        this.commands = commands;
        this.serviceProvider = serviceProvider;
        this.dbContext = dbContext;
    }

    [Name("Pin")]
    [Summary("Pins a message.")]
    [Command("pin")]
    [RateLimit(5, 30)]
    [RequireUserPermission(GuildPermission.ManageMessages)]
    [RequireDbGuild]
    public async Task PinAsync([Remainder] string? _ = null)
    {
        // Get guild from db
        Guild? guild = Context.DbGuild!;

        // Check if the guild has a pins channel set
        if (guild.PinsChannelId == 0)
        {
            await ReplyAsync("Pins channel hasn't been set yet.");
            return;
        }

        // Get the pins channel
        SocketTextChannel? pinsChannel = Context.Guild.GetTextChannel(guild.PinsChannelId);

        if (pinsChannel == null)
        {
            await ReplyAsync("Pins channel couldn't be found.");
            return;
        }

        // Get the message the user replied to
        var message = await Context.Channel.GetMessageAsync(Context.Message.ReferencedMessage.Id) as IUserMessage;

        if (message == null)
        {
            await ReplyAsync("Couldn't find the message you want to pin.");
            return;
        }

        // Make an embed of the message details
        EmbedBuilder embed = new()
        {
            Title = $"Pin in `#{message.Channel.Name}` by {Context.Message.Author.Username}",
            Url = message.GetJumpUrl(),
            Author = new EmbedAuthorBuilder()
            {
                Name = message.Author.Username,
                IconUrl = message.Author.GetAvatarUrl()
            },
            Description = message.Content,
            Color = Colors.Blue,
            Timestamp = message.CreatedAt
        };

        // Add image to embed
        if (message.Attachments.Count > 0)
            embed.ImageUrl = message.Attachments.First().Url;

        // Send the message to the pins channel
        await pinsChannel.SendMessageAsync(embed: embed.Build());

        // Send a confirmation message
        await ReplyAsync("Message pinned successfully.");

        return;
    }
}
