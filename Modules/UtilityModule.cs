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
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    public async Task PinAsync([Remainder] string? _ = null)
    {
        // Get guild from db
        Guild? guild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == Context.Guild.Id);

        if (guild == null)
        {
            await ReplyAsync("Your guild hasn't been added to the database yet, please try again.");
            return;
        }

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

    [Name("Download Emojis")]
    [Summary("Downloads all emojis from the server and packs them into a ZIP file.")]
    [Command("downloademojis")]
    [Alias("downloademoji", "downloademotes", "downloademote")]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RateLimit(1, 600)]
    public async Task DownloadEmojisAsync()
    {
        SocketGuild guild = Context.Guild;
        string directory = Path.Combine(Path.GetTempPath(), "emojis", guild.Id.ToString());
        string zipPath = Path.Combine(Path.GetTempPath(), $"{guild.Name}_Emojis.zip");

        // Ensure directory is clean
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);

        Directory.CreateDirectory(directory);

        using HttpClient client = new();
        int totalEmojis = guild.Emotes.Count;
        int count = 0;

        var progressMessage = await ReplyAsync($"Starting to download {totalEmojis} emojis...");

        foreach (var emoji in guild.Emotes)
        {
            string extension = emoji.Animated ? ".gif" : ".png";
            string filePath = Path.Combine(directory, emoji.Name + extension);

            byte[] data = await client.GetByteArrayAsync(emoji.Url);
            await File.WriteAllBytesAsync(filePath, data);
            count++;
        }

        await progressMessage.ModifyAsync(m => m.Content = "Packing emojis into a ZIP file...");

        // Create ZIP archive
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        ZipFile.CreateFromDirectory(directory, zipPath);

        await progressMessage.ModifyAsync(m => m.Content = "Uploading ZIP file...");
        await Context.Channel.SendFileAsync(zipPath, "Here are all the server emojis!");

        // Clean up files
        Directory.Delete(directory, true);
        File.Delete(zipPath);

        await progressMessage.ModifyAsync(m => m.Content = "Emoji download process completed!");
    }
}
