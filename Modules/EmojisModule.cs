using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Extensions;
using Morpheus.Handlers;
using Morpheus.Services;
using System.IO.Compression;

namespace Morpheus.Modules;

public class EmojisModule(DiscordSocketClient client, CommandService commands, InteractionsHandler interactionHandler, IServiceProvider serviceProvider, DB dbContext, LogsService logsService) : ModuleBase<SocketCommandContextExtended>
{
    private static readonly HttpClient httpClient = new();

    private readonly CommandService commands = commands;
    private readonly IServiceProvider serviceProvider = serviceProvider;
    private readonly DB dbContext = dbContext;
    private readonly LogsService logsService = logsService;

    [Name("Use Emoji")]
    [Summary("Uses an emoji in the current channel. The bot will try to delete your original message at the end.")]
    [Command("emoji")]
    [Alias("emote")]
    [RequireBotPermission(GuildPermission.UseExternalEmojis)]
    [RequireUserPermission(GuildPermission.UseExternalEmojis)]
    [RateLimit(5, 10)]
    public async Task UseEmoji([Remainder] string emojiName)
    {
        Emote? emoji = (await Context.Client.Rest.GetApplicationEmotesAsync()).FirstOrDefault(e => e.Name.Equals(emojiName, StringComparison.CurrentCultureIgnoreCase));

        if (emoji == null)
        {
            await ReplyAsync($"Custom emoji '{emojiName}' not found.");
            return;
        }

        // Send the emoji in the channel
        await Context.Channel.SendMessageAsync(emoji.ToString());

        try
        {
            await Context.Message.DeleteAsync();
        }
        catch (Exception ex)
        {
            logsService.Log($"Failed to delete user message after emoji send: {ex}", LogSeverity.Warning);
        }
    }

    [Name("Emoji React")]
    [Summary("React to the message you replied to with the specified emoji. The bot will try to delete your original message at the end.")]
    [Command("react")]
    [Alias("reactemoji", "reactemote", "reactemojis", "reactemotes")]
    [RequireBotPermission(GuildPermission.AddReactions)]
    [RequireUserPermission(GuildPermission.AddReactions)]
    [RateLimit(5, 10)]
    public async Task React([Remainder] string emojiName)
    {
        Emote? emoji = (await Context.Client.Rest.GetApplicationEmotesAsync()).FirstOrDefault(e => e.Name.Equals(emojiName, StringComparison.CurrentCultureIgnoreCase));

        if (emoji == null)
        {
            await ReplyAsync($"Custom emoji '{emojiName}' not found.");
            return;
        }

        // Get the message the user replied to

        if (await Context.Channel.GetMessageAsync(Context.Message.ReferencedMessage.Id) is not IUserMessage message)
        {
            await ReplyAsync("You need to reply to a message to react to it.");
            return;
        }

        // React to the message with the specified emoji
        await message.AddReactionAsync(emoji);

        try
        {
            await Context.Message.DeleteAsync();
        }
        catch (Exception ex)
        {
            logsService.Log($"Failed to delete user message after react: {ex}", LogSeverity.Warning);
        }
    }

    [Name("List Emojis")]
    [Summary("Lists all custom emojis that can be used by the bot.")]
    [Command("listemojis")]
    [Alias("listemotes", "listemoji", "listemote")]
    [RateLimit(5, 10)]
    public async Task ListEmojis()
    {
        IReadOnlyCollection<Emote> emotes = await Context.Client.Rest.GetApplicationEmotesAsync();

        if (emotes.Count == 0)
        {
            await ReplyAsync("No custom emojis found.");
            return;
        }

        string emojiList = string.Join("\n", emotes.Select(e => e.Name + " - " + e.ToString()));
        await ReplyAsync($"**Custom Emojis:**\n{emojiList}");
    }

    [Name("Download Emojis")]
    [Summary("Downloads all emojis from the server and packs them into a ZIP file.")]
    [Command("downloademojis")]
    [Alias("downloademoji", "downloademotes", "downloademote")]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireBotPermission(GuildPermission.AttachFiles)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(1, 600)]
    public async Task DownloadEmojis()
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

        IUserMessage progressMessage = await ReplyAsync($"Starting to download {totalEmojis} emojis...");

        foreach (GuildEmote? emoji in guild.Emotes)
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
