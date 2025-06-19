using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Morpheus.Attributes;
using Morpheus.Database.Models;
using Morpheus.Extensions;
using Morpheus.Utilities;

namespace Morpheus.Modules;

public class UtilityModule() : ModuleBase<SocketCommandContextExtended>
{
    private static readonly HttpClient httpClient = new();

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
        if (await Context.Channel.GetMessageAsync(Context.Message.ReferencedMessage.Id) is not IUserMessage message)
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
