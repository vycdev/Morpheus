using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Extensions;
using Morpheus.Handlers;
using Morpheus.Services;
using Morpheus.Utilities;
using System.IO.Compression;

namespace Morpheus.Modules;

public class EmojisModule : ModuleBase<SocketCommandContextExtended>
{
    private static readonly HttpClient httpClient = new();

    private readonly DiscordSocketClient client;
    private readonly CommandService commands;
    private readonly IServiceProvider serviceProvider;
    private readonly DB dbContext;
    private readonly LogsService logsService;

    // Track pending import sessions: messageId -> session data
    private static readonly Dictionary<ulong, EmojiImportSession> _importSessions = [];

    private class EmojiImportSession
    {
        public ulong UserId { get; set; }
        public ulong SourceGuildId { get; set; }
        public ulong TargetGuildId { get; set; }
        public int EmojiPage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public EmojisModule(DiscordSocketClient client, CommandService commands, InteractionsHandler interactionHandler, IServiceProvider serviceProvider, DB dbContext, LogsService logsService)
    {
        this.client = client;
        this.commands = commands;
        this.serviceProvider = serviceProvider;
        this.dbContext = dbContext;
        this.logsService = logsService;

        interactionHandler.RegisterInteraction("emoji_import_server", HandleServerSelectInteraction);
        interactionHandler.RegisterInteraction("emoji_import_select", HandleEmojiSelectInteraction);
        interactionHandler.RegisterInteraction("emoji_import_page", HandleEmojiPageInteraction);
        interactionHandler.RegisterInteraction("emoji_import_cancel", HandleCancelInteraction);
        interactionHandler.RegisterInteraction("emoji_import_back", HandleBackToServersInteraction);
        interactionHandler.RegisterInteraction("emoji_import_another", HandleImportAnotherInteraction);
        interactionHandler.RegisterInteraction("emoji_import_all", HandleImportAllInteraction);
    }

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

    // ─── Import Emoji Command ────────────────────────────────────────────

    private const int EmojiPageSize = 25; // Discord select menu max options
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(5);

    [Name("Import Emoji")]
    [Summary("Import an emoji from another server the bot is in. Opens an interactive menu to pick the server and emoji.")]
    [Command("importemoji")]
    [Alias("importemote", "stealemoji", "stealemote")]
    [RequireUserPermission(GuildPermission.ManageEmojisAndStickers)]
    [RequireBotPermission(GuildPermission.ManageEmojisAndStickers)]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 30)]
    public async Task ImportEmoji()
    {
        // Gather guilds the bot shares with the invoking user (excluding the current guild)
        var sharedGuilds = client.Guilds
            .Where(g => g.Id != Context.Guild.Id && g.GetUser(Context.User.Id) != null)
            .OrderBy(g => g.Name)
            .ToList();

        if (sharedGuilds.Count == 0)
        {
            await ReplyAsync("I don't share any other servers with you to import emojis from.");
            return;
        }

        // Build the server selection menu (max 25 options per select menu)
        var serverMenu = new SelectMenuBuilder()
            .WithPlaceholder("Select a server to browse emojis from")
            .WithCustomId("emoji_import_server")
            .WithMinValues(1)
            .WithMaxValues(1);

        foreach (var guild in sharedGuilds.Take(EmojiPageSize))
        {
            int emojiCount = guild.Emotes.Count;
            serverMenu.AddOption(
                label: guild.Name.Length > 100 ? guild.Name[..100] : guild.Name,
                value: guild.Id.ToString(),
                description: $"{emojiCount} emoji{(emojiCount != 1 ? "s" : "")}"
            );
        }

        var embed = new EmbedBuilder()
            .WithColor(Colors.Blue)
            .WithTitle("📥 Import Emoji")
            .WithDescription("Select a server to browse its emojis. You can then pick one to import into this server.")
            .WithFooter("This menu will expire in 5 minutes.")
            .Build();

        var cancelButton = new ButtonBuilder()
            .WithLabel("Cancel")
            .WithCustomId("emoji_import_cancel")
            .WithStyle(ButtonStyle.Danger);

        var components = new ComponentBuilder()
            .WithSelectMenu(serverMenu)
            .WithButton(cancelButton)
            .Build();

        var message = await ReplyAsync(embed: embed, components: components);

        // Store session
        _importSessions[message.Id] = new EmojiImportSession
        {
            UserId = Context.User.Id,
            TargetGuildId = Context.Guild.Id
        };

        CleanupExpiredSessions();
    }

    // ─── Interaction: Server Selected ────────────────────────────────────

    private async Task HandleServerSelectInteraction(SocketInteraction interaction)
    {
        if (interaction is not SocketMessageComponent comp) return;
        if (comp.Data.CustomId != "emoji_import_server") return;

        ulong messageId = comp.Message.Id;
        if (!_importSessions.TryGetValue(messageId, out var session) || session.UserId != comp.User.Id)
        {
            await comp.RespondAsync("This menu isn't for you, or it has expired.", ephemeral: true);
            return;
        }

        if (DateTime.UtcNow - session.CreatedAt > SessionTimeout)
        {
            _importSessions.Remove(messageId);
            await comp.RespondAsync("This import session has expired. Please start a new one.", ephemeral: true);
            return;
        }

        await comp.DeferAsync();

        ulong selectedGuildId = ulong.Parse(comp.Data.Values.First());
        var sourceGuild = client.GetGuild(selectedGuildId);

        if (sourceGuild == null)
        {
            await comp.FollowupAsync("I can no longer access that server.", ephemeral: true);
            return;
        }

        session.SourceGuildId = selectedGuildId;
        session.EmojiPage = 0;

        var emojis = sourceGuild.Emotes.OrderBy(e => e.Name).ToList();

        if (emojis.Count == 0)
        {
            await comp.ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = new EmbedBuilder()
                    .WithColor(Colors.Blue)
                    .WithTitle("📥 Import Emoji")
                    .WithDescription($"**{sourceGuild.Name}** has no custom emojis.")
                    .Build();
                msg.Components = new ComponentBuilder().Build();
            });
            _importSessions.Remove(messageId);
            return;
        }

        await UpdateEmojiSelectionMessage(comp, session, sourceGuild, emojis);
    }

    // ─── Interaction: Emoji Selected ─────────────────────────────────────

    private async Task HandleEmojiSelectInteraction(SocketInteraction interaction)
    {
        if (interaction is not SocketMessageComponent comp) return;
        if (comp.Data.CustomId != "emoji_import_select") return;

        ulong messageId = comp.Message.Id;
        if (!_importSessions.TryGetValue(messageId, out var session) || session.UserId != comp.User.Id)
        {
            await comp.RespondAsync("This menu isn't for you, or it has expired.", ephemeral: true);
            return;
        }

        if (DateTime.UtcNow - session.CreatedAt > SessionTimeout)
        {
            _importSessions.Remove(messageId);
            await comp.RespondAsync("This import session has expired. Please start a new one.", ephemeral: true);
            return;
        }

        await comp.DeferAsync();

        ulong emojiId = ulong.Parse(comp.Data.Values.First());
        var sourceGuild = client.GetGuild(session.SourceGuildId);
        var targetGuild = client.GetGuild(session.TargetGuildId);

        if (sourceGuild == null || targetGuild == null)
        {
            await comp.FollowupAsync("One of the servers is no longer accessible.", ephemeral: true);
            _importSessions.Remove(messageId);
            return;
        }

        var emoji = sourceGuild.Emotes.FirstOrDefault(e => e.Id == emojiId);
        if (emoji == null)
        {
            await comp.FollowupAsync("That emoji no longer exists in the source server.", ephemeral: true);
            return;
        }

        // Check emoji slot limits (fetch fresh data via REST to avoid stale cache)
        var freshEmotes = (await client.Rest.GetGuildAsync(session.TargetGuildId))?.Emotes
            ?? targetGuild.Emotes;
        int staticCount = freshEmotes.Count(e => !e.Animated);
        int animatedCount = freshEmotes.Count(e => e.Animated);
        int emojiLimit = GetGuildEmojiLimit(targetGuild.PremiumTier);

        if (emoji.Animated && animatedCount >= emojiLimit)
        {
            await comp.FollowupAsync($"This server has reached its animated emoji limit ({animatedCount}/{emojiLimit}). Boost the server to unlock more slots.", ephemeral: true);
            return;
        }
        if (!emoji.Animated && staticCount >= emojiLimit)
        {
            await comp.FollowupAsync($"This server has reached its static emoji limit ({staticCount}/{emojiLimit}). Boost the server to unlock more slots.", ephemeral: true);
            return;
        }

        // Auto-suffix if emoji name already exists
        string finalName = emoji.Name;
        if (targetGuild.Emotes.Any(e => e.Name.Equals(finalName, StringComparison.OrdinalIgnoreCase)))
        {
            int suffix = 2;
            while (targetGuild.Emotes.Any(e => e.Name.Equals($"{emoji.Name}_{suffix}", StringComparison.OrdinalIgnoreCase)))
                suffix++;
            finalName = $"{emoji.Name}_{suffix}";
        }

        try
        {
            // Download the emoji image
            string url = emoji.Url;
            byte[] imageData = await httpClient.GetByteArrayAsync(url);
            using var stream = new MemoryStream(imageData);
            var image = new Image(stream);

            // Add the emoji to the target guild
            var newEmoji = await targetGuild.CreateEmoteAsync(finalName, image);

            string renamedNote = finalName != emoji.Name ? $" (renamed to **{finalName}** to avoid conflict)" : "";

            // Build success components with "Import Another" button
            var successComponents = new ComponentBuilder()
                .WithButton("Import Another", "emoji_import_another", ButtonStyle.Primary)
                .WithButton("Done", "emoji_import_cancel", ButtonStyle.Secondary)
                .Build();

            // Update the message to show success
            await comp.ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = new EmbedBuilder()
                    .WithColor(Colors.Blue)
                    .WithTitle("✅ Emoji Imported!")
                    .WithDescription($"Successfully imported {newEmoji} **{newEmoji.Name}** from **{sourceGuild.Name}**.{renamedNote}")
                    .WithThumbnailUrl(newEmoji.Url)
                    .Build();
                msg.Components = successComponents;
            });

            logsService.Log($"Emoji '{finalName}' imported from {sourceGuild.Name} to {targetGuild.Name} by {comp.User}", LogSeverity.Info);
        }
        catch (Exception ex)
        {
            logsService.Log($"Failed to import emoji '{emoji.Name}': {ex}", LogSeverity.Warning);
            await comp.FollowupAsync($"Failed to import the emoji: {ex.Message}", ephemeral: true);
        }
    }

    // ─── Interaction: Page Navigation ────────────────────────────────────

    private async Task HandleEmojiPageInteraction(SocketInteraction interaction)
    {
        if (interaction is not SocketMessageComponent comp) return;
        if (!comp.Data.CustomId.StartsWith("emoji_import_page:")) return;

        ulong messageId = comp.Message.Id;
        if (!_importSessions.TryGetValue(messageId, out var session) || session.UserId != comp.User.Id)
        {
            await comp.RespondAsync("This menu isn't for you, or it has expired.", ephemeral: true);
            return;
        }

        if (DateTime.UtcNow - session.CreatedAt > SessionTimeout)
        {
            _importSessions.Remove(messageId);
            await comp.RespondAsync("This import session has expired. Please start a new one.", ephemeral: true);
            return;
        }

        await comp.DeferAsync();

        string direction = comp.Data.CustomId.Split(':')[1];
        session.EmojiPage += direction == "next" ? 1 : -1;
        if (session.EmojiPage < 0) session.EmojiPage = 0;

        var sourceGuild = client.GetGuild(session.SourceGuildId);
        if (sourceGuild == null)
        {
            await comp.FollowupAsync("The source server is no longer accessible.", ephemeral: true);
            _importSessions.Remove(messageId);
            return;
        }

        var emojis = sourceGuild.Emotes.OrderBy(e => e.Name).ToList();
        await UpdateEmojiSelectionMessage(comp, session, sourceGuild, emojis);
    }

    // ─── Interaction: Cancel ─────────────────────────────────────────────

    private async Task HandleCancelInteraction(SocketInteraction interaction)
    {
        if (interaction is not SocketMessageComponent comp) return;
        if (comp.Data.CustomId != "emoji_import_cancel") return;

        ulong messageId = comp.Message.Id;
        if (!_importSessions.TryGetValue(messageId, out var session) || session.UserId != comp.User.Id)
        {
            await comp.RespondAsync("This menu isn't for you.", ephemeral: true);
            return;
        }

        _importSessions.Remove(messageId);

        await comp.DeferAsync();
        await comp.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = new EmbedBuilder()
                .WithColor(Colors.Blue)
                .WithTitle("📥 Import Emoji")
                .WithDescription("Import cancelled.")
                .Build();
            msg.Components = new ComponentBuilder().Build();
        });
    }

    // ─── Helper: Build emoji selection message ───────────────────────────

    private async Task UpdateEmojiSelectionMessage(SocketMessageComponent comp, EmojiImportSession session, SocketGuild sourceGuild, List<GuildEmote> emojis)
    {
        int totalPages = (int)Math.Ceiling(emojis.Count / (double)EmojiPageSize);
        if (session.EmojiPage >= totalPages) session.EmojiPage = totalPages - 1;

        var pageEmojis = emojis
            .Skip(session.EmojiPage * EmojiPageSize)
            .Take(EmojiPageSize)
            .ToList();

        var emojiMenu = new SelectMenuBuilder()
            .WithPlaceholder("Select an emoji to import")
            .WithCustomId("emoji_import_select")
            .WithMinValues(1)
            .WithMaxValues(1);

        foreach (var emoji in pageEmojis)
        {
            string label = emoji.Name.Length > 100 ? emoji.Name[..100] : emoji.Name;
            string desc = emoji.Animated ? "Animated" : "Static";
            emojiMenu.AddOption(label: label, value: emoji.Id.ToString(), description: desc, emote: emoji);
        }

        // Build the embed showing emoji previews for this page
        string emojiPreview = string.Join(" ", pageEmojis.Select(e => e.ToString()));
        var embed = new EmbedBuilder()
            .WithColor(Colors.Blue)
            .WithTitle($"📥 Import Emoji from {sourceGuild.Name}")
            .WithDescription($"Select an emoji to import into this server.\n\n{emojiPreview}")
            .WithFooter($"Page {session.EmojiPage + 1}/{totalPages} • {emojis.Count} emoji{(emojis.Count != 1 ? "s" : "")} total")
            .Build();

        var componentBuilder = new ComponentBuilder()
            .WithSelectMenu(emojiMenu);

        // Add pagination buttons if needed
        if (totalPages > 1)
        {
            componentBuilder.WithButton("◀ Previous", "emoji_import_page:prev", ButtonStyle.Secondary, disabled: session.EmojiPage == 0);
            componentBuilder.WithButton("Next ▶", "emoji_import_page:next", ButtonStyle.Secondary, disabled: session.EmojiPage >= totalPages - 1);
        }

        componentBuilder.WithButton("Import All", "emoji_import_all", ButtonStyle.Success);
        componentBuilder.WithButton("◀ Back to Servers", "emoji_import_back", ButtonStyle.Primary);
        componentBuilder.WithButton("Cancel", "emoji_import_cancel", ButtonStyle.Danger);

        await comp.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = componentBuilder.Build();
        });
    }

    // ─── Interaction: Back to Servers ─────────────────────────────────

    private async Task HandleBackToServersInteraction(SocketInteraction interaction)
    {
        if (interaction is not SocketMessageComponent comp) return;
        if (comp.Data.CustomId != "emoji_import_back") return;

        ulong messageId = comp.Message.Id;
        if (!_importSessions.TryGetValue(messageId, out var session) || session.UserId != comp.User.Id)
        {
            await comp.RespondAsync("This menu isn't for you, or it has expired.", ephemeral: true);
            return;
        }

        if (DateTime.UtcNow - session.CreatedAt > SessionTimeout)
        {
            _importSessions.Remove(messageId);
            await comp.RespondAsync("This import session has expired. Please start a new one.", ephemeral: true);
            return;
        }

        await comp.DeferAsync();

        // Rebuild the server selection menu
        var sharedGuilds = client.Guilds
            .Where(g => g.Id != session.TargetGuildId && g.GetUser(session.UserId) != null)
            .OrderBy(g => g.Name)
            .Take(EmojiPageSize)
            .ToList();

        var serverMenu = new SelectMenuBuilder()
            .WithPlaceholder("Select a server to browse emojis from")
            .WithCustomId("emoji_import_server")
            .WithMinValues(1)
            .WithMaxValues(1);

        foreach (var guild in sharedGuilds)
        {
            int emojiCount = guild.Emotes.Count;
            serverMenu.AddOption(
                label: guild.Name.Length > 100 ? guild.Name[..100] : guild.Name,
                value: guild.Id.ToString(),
                description: $"{emojiCount} emoji{(emojiCount != 1 ? "s" : "")}"
            );
        }

        var embed = new EmbedBuilder()
            .WithColor(Colors.Blue)
            .WithTitle("📥 Import Emoji")
            .WithDescription("Select a server to browse its emojis. You can then pick one to import into this server.")
            .WithFooter("This menu will expire in 5 minutes.")
            .Build();

        var components = new ComponentBuilder()
            .WithSelectMenu(serverMenu)
            .WithButton("Cancel", "emoji_import_cancel", ButtonStyle.Danger)
            .Build();

        session.SourceGuildId = 0;
        session.EmojiPage = 0;

        await comp.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = components;
        });
    }

    // ─── Interaction: Import Another ─────────────────────────────────────

    private async Task HandleImportAnotherInteraction(SocketInteraction interaction)
    {
        if (interaction is not SocketMessageComponent comp) return;
        if (comp.Data.CustomId != "emoji_import_another") return;

        ulong messageId = comp.Message.Id;
        if (!_importSessions.TryGetValue(messageId, out var session) || session.UserId != comp.User.Id)
        {
            await comp.RespondAsync("This menu isn't for you, or it has expired.", ephemeral: true);
            return;
        }

        if (DateTime.UtcNow - session.CreatedAt > SessionTimeout)
        {
            _importSessions.Remove(messageId);
            await comp.RespondAsync("This import session has expired. Please start a new one.", ephemeral: true);
            return;
        }

        await comp.DeferAsync();

        // Go back to the emoji list of the same source server, keeping the same page
        var sourceGuild = client.GetGuild(session.SourceGuildId);
        if (sourceGuild == null || session.SourceGuildId == 0)
        {
            // Source guild no longer available, fall back to server selection
            await comp.FollowupAsync("The source server is no longer accessible. Returning to server selection.", ephemeral: true);
            // Trigger back to servers logic by resetting
            session.SourceGuildId = 0;
            session.EmojiPage = 0;
            return;
        }

        var emojis = sourceGuild.Emotes.OrderBy(e => e.Name).ToList();

        if (emojis.Count == 0)
        {
            await comp.ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = new EmbedBuilder()
                    .WithColor(Colors.Blue)
                    .WithTitle("📥 Import Emoji")
                    .WithDescription($"**{sourceGuild.Name}** has no more custom emojis.")
                    .Build();
                msg.Components = new ComponentBuilder().Build();
            });
            _importSessions.Remove(messageId);
            return;
        }

        await UpdateEmojiSelectionMessage(comp, session, sourceGuild, emojis);
    }

    // ─── Interaction: Import All ─────────────────────────────────────────

    private async Task HandleImportAllInteraction(SocketInteraction interaction)
    {
        if (interaction is not SocketMessageComponent comp) return;
        if (comp.Data.CustomId != "emoji_import_all") return;

        ulong messageId = comp.Message.Id;
        if (!_importSessions.TryGetValue(messageId, out var session) || session.UserId != comp.User.Id)
        {
            await comp.RespondAsync("This menu isn't for you, or it has expired.", ephemeral: true);
            return;
        }

        if (DateTime.UtcNow - session.CreatedAt > SessionTimeout)
        {
            _importSessions.Remove(messageId);
            await comp.RespondAsync("This import session has expired. Please start a new one.", ephemeral: true);
            return;
        }

        await comp.DeferAsync();

        var sourceGuild = client.GetGuild(session.SourceGuildId);
        var targetGuild = client.GetGuild(session.TargetGuildId);

        if (sourceGuild == null || targetGuild == null)
        {
            await comp.FollowupAsync("One of the servers is no longer accessible.", ephemeral: true);
            _importSessions.Remove(messageId);
            return;
        }

        var emojis = sourceGuild.Emotes.OrderBy(e => e.Name).ToList();
        int emojiLimit = GetGuildEmojiLimit(targetGuild.PremiumTier);
        int imported = 0;
        int skipped = 0;
        int failed = 0;
        var importedNames = new List<string>();

        // Show progress embed
        await comp.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = new EmbedBuilder()
                .WithColor(Colors.Blue)
                .WithTitle("📥 Importing All Emojis...")
                .WithDescription($"Importing **{emojis.Count}** emojis from **{sourceGuild.Name}**...\nThis may take a while.")
                .Build();
            msg.Components = new ComponentBuilder().Build();
        });

        foreach (var emoji in emojis)
        {
            // Check slot limits
            int staticCount = targetGuild.Emotes.Count(e => !e.Animated);
            int animatedCount = targetGuild.Emotes.Count(e => e.Animated);

            if ((emoji.Animated && animatedCount >= emojiLimit) || (!emoji.Animated && staticCount >= emojiLimit))
            {
                skipped++;
                continue;
            }

            // Auto-suffix if name exists
            string finalName = emoji.Name;
            if (targetGuild.Emotes.Any(e => e.Name.Equals(finalName, StringComparison.OrdinalIgnoreCase)))
            {
                int suffix = 2;
                while (targetGuild.Emotes.Any(e => e.Name.Equals($"{emoji.Name}_{suffix}", StringComparison.OrdinalIgnoreCase)))
                    suffix++;
                finalName = $"{emoji.Name}_{suffix}";
            }

            try
            {
                byte[] imageData = await httpClient.GetByteArrayAsync(emoji.Url);
                using var stream = new MemoryStream(imageData);
                var image = new Image(stream);
                await targetGuild.CreateEmoteAsync(finalName, image);
                imported++;
                importedNames.Add(finalName);
            }
            catch
            {
                failed++;
            }

            // Update progress every 5 emojis
            if ((imported + skipped + failed) % 5 == 0)
            {
                try
                {
                    await comp.ModifyOriginalResponseAsync(msg =>
                    {
                        msg.Embed = new EmbedBuilder()
                            .WithColor(Colors.Blue)
                            .WithTitle("📥 Importing All Emojis...")
                            .WithDescription($"Progress: **{imported + skipped + failed}/{emojis.Count}**\n✅ Imported: {imported} | ⏭ Skipped: {skipped} | ❌ Failed: {failed}")
                            .Build();
                    });
                }
                catch { /* rate limited, ignore */ }
            }
        }

        // Final result
        var resultEmbed = new EmbedBuilder()
            .WithColor(Colors.Blue)
            .WithTitle("✅ Bulk Import Complete!")
            .WithDescription($"Imported emojis from **{sourceGuild.Name}**.")
            .AddField("Imported", imported.ToString(), true)
            .AddField("Skipped (limit)", skipped.ToString(), true)
            .AddField("Failed", failed.ToString(), true);

        if (importedNames.Count > 0 && importedNames.Count <= 20)
            resultEmbed.AddField("Emojis", string.Join(", ", importedNames));
        else if (importedNames.Count > 20)
            resultEmbed.AddField("Emojis", string.Join(", ", importedNames.Take(20)) + $" and {importedNames.Count - 20} more...");

        var doneComponents = new ComponentBuilder()
            .WithButton("Done", "emoji_import_cancel", ButtonStyle.Secondary)
            .Build();

        await comp.ModifyOriginalResponseAsync(msg =>
        {
            msg.Embed = resultEmbed.Build();
            msg.Components = doneComponents;
        });

        logsService.Log($"Bulk emoji import from {sourceGuild.Name} to {targetGuild.Name} by {comp.User}: {imported} imported, {skipped} skipped, {failed} failed", LogSeverity.Info);
    }

    // ─── Helper: Get guild emoji limit based on boost tier ───────────────

    private static int GetGuildEmojiLimit(PremiumTier tier) => tier switch
    {
        PremiumTier.Tier1 => 100,
        PremiumTier.Tier2 => 150,
        PremiumTier.Tier3 => 250,
        _ => 50
    };

    // ─── Cleanup expired sessions ────────────────────────────────────────

    private static void CleanupExpiredSessions()
    {
        var expiredKeys = _importSessions
            .Where(kvp => DateTime.UtcNow - kvp.Value.CreatedAt > SessionTimeout)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
            _importSessions.Remove(key);
    }
}
