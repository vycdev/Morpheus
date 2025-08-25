using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Morpheus.Services;
using Morpheus.Utilities.Lists;
using System.Text.RegularExpressions;

namespace Morpheus.Handlers;

public class FunnyResponsesHandler
{
    private readonly DiscordSocketClient client;
    private readonly UsersService usersService;
    private readonly GuildService guildService;
    private readonly LogsService logsService;
    private readonly bool started = false;

    private static readonly RandomBag codifyResponsesBag = new(FunnyResponses.ResponsesToCodifyMentions);
    private static readonly RandomBag morpheusResponsesBag = new(FunnyResponses.ResponsesToMorpheusMentions);

    private readonly Random random = new();

    public FunnyResponsesHandler(DiscordSocketClient client, UsersService usersService, GuildService guildService, LogsService logsService)
    {
        if (started)
            throw new InvalidOperationException("At most one instance of this service can be started");

        started = true;

        this.client = client;
        this.usersService = usersService;
        this.guildService = guildService;
        this.logsService = logsService;

        // Subscribe to incoming messages
        client.MessageReceived += HandleMessageAsync;
    }

    private async Task HandleMessageAsync(SocketMessage messageParam)
    {
        // Only handle user messages
        if (messageParam is not SocketUserMessage message)
            return;

        // Ignore bots
        if (message.Author.IsBot)
            return;

        var content = (message.Content ?? string.Empty).Trim();

        // Detect keyword matches (whole words, case-insensitive)
        var matches = new System.Collections.Generic.List<string>();
        if (Regex.IsMatch(content, "\\bcodify\\b", RegexOptions.IgnoreCase))
            matches.Add("codify");
        if (Regex.IsMatch(content, "\\bmorpheus\\b", RegexOptions.IgnoreCase))
            matches.Add("morpheus");

        if (matches.Count == 0)
            return;

        // If both are present pick one at random so we only ever reply once
        string chosen = matches.Count == 1 ? matches[0] : matches[random.Next(matches.Count)];

        // 50% chance to respond
        if (random.NextDouble() >= 0.10)
            return;

        string reply = chosen switch
        {
            "codify" => codifyResponsesBag.Random(),
            "morpheus" => morpheusResponsesBag.Random(),
            _ => throw new InvalidOperationException("Unexpected keyword match")
        };

        try
        {
            await message.ReplyAsync(reply, allowedMentions: AllowedMentions.None);
        }
        catch (Exception ex)
        {
            logsService.Log($"FunnyResponsesHandler: failed to send reply: {ex.Message}");
        }
    }
}
