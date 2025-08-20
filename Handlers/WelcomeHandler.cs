using Discord;
using Discord.WebSocket;
using Morpheus.Database.Models;
using Morpheus.Services;
using Morpheus.Utilities;
using Morpheus.Utilities.Lists;

namespace Morpheus.Handlers;

public class WelcomeHandler
{
    private readonly DiscordSocketClient client;
    private readonly GuildService guildService;
    private readonly bool started = false;

    private readonly RandomBag welcomeMessagesBag = new(WelcomeMessages.Messages);
    private readonly RandomBag goodbyeMessagesBag = new(GoodbyeMessages.Messages);
    private readonly RandomBag happyEmojisBag = new(EmojiList.EmojisHappy);
    private readonly RandomBag sadEmojisBag = new(EmojiList.EmojisSad);

    public WelcomeHandler(DiscordSocketClient client, GuildService guildService)
    {
        if (started)
            throw new InvalidOperationException("At most one instance of this service can be started");

        started = true;

        this.client = client;
        this.guildService = guildService;

        client.UserJoined += HandleUserJoined;
        client.UserLeft += HandleUserLeft;
    }

    private async Task HandleUserJoined(SocketGuildUser user)
    {
        Guild? guild = await guildService.TryGetCreateGuild(user.Guild);

        if (guild == null)
            return;

        if (guild.WelcomeChannelId == 0)
            return;

        SocketTextChannel channel = user.Guild.GetTextChannel(guild.WelcomeChannelId);

        if (channel == null)
            return;


        Emote? joinEmoji = null;

        if (ulong.TryParse(Env.Variables?["CUSTOM_JOIN_EMOTE_ID"], out ulong emojiId))
            joinEmoji = await client.Rest.GetApplicationEmoteAsync(emojiId);

        await channel.SendMessageAsync((joinEmoji != null ? joinEmoji.ToString() + " " : "") + string.Format(welcomeMessagesBag.Random(), user.Mention));
        await channel.SendMessageAsync($"Server now has {user.Guild.MemberCount} members! {happyEmojisBag.Random()}");
    }

    private async Task HandleUserLeft(SocketGuild guild, SocketUser user)
    {
        Guild? guildDb = await guildService.TryGetCreateGuild(guild);

        if (guildDb == null)
            return;

        if (guildDb.WelcomeChannelId == 0)
            return;

        SocketTextChannel channel = guild.GetTextChannel(guildDb.WelcomeChannelId);

        if (channel == null)
            return;

        Emote? leaveEmoji = null;

        if (ulong.TryParse(Env.Variables?["CUSTOM_LEAVE_EMOTE_ID"], out ulong emojiId))
            leaveEmoji = await client.Rest.GetApplicationEmoteAsync(emojiId);

        await channel.SendMessageAsync((leaveEmoji != null ? leaveEmoji.ToString() + " " : "") + string.Format(goodbyeMessagesBag.Random(), user.Mention));
        await channel.SendMessageAsync($"Server now has {guild.MemberCount} members! {sadEmojisBag.Random()}");
    }
}
