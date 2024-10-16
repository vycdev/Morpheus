﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Morpheus.Utilities;
using System.Text;

namespace Morpheus.Commands;

public class MiscModule : ModuleBase<SocketCommandContext>
{
    private readonly CommandService commands;
    private readonly IServiceProvider serviceProvider;

    public MiscModule(DiscordSocketClient client, CommandService commands, IServiceProvider serviceProvider)
    {
        this.commands = commands;
        this.serviceProvider = serviceProvider;

        client.InteractionCreated += HandleInteraction;
    }

    [Name("Help")]
    [Summary("Displays a list of commands.")]
    [Command("help")]
    [Alias("commands", "cmds", "h")]
    public async Task HelpAsync()
    {
        var builder = new EmbedBuilder()
        {
            Color = new Color(114, 137, 218),
            Description = "Select a command module to view its commands."
        };

        // Create the selector (dropdown)
        var modules = commands.Modules.Select(m => m.Name).ToList();
        var selectMenu = new SelectMenuBuilder()
            .WithPlaceholder("Select a module")
            .WithCustomId("module_selector");

        // Add the options
        foreach (string? module in modules)
            selectMenu.AddOption(new SelectMenuOptionBuilder().WithLabel(module.Replace("Module", "")).WithValue(module));

        // Create an interaction message
        var component = new ComponentBuilder().WithSelectMenu(selectMenu).Build();

        // Create the initial embed
        var message = await ReplyAsync(embed: builder.Build());

        await message.ModifyAsync(msg => msg.Components = component);
    }

    // Handle the interaction when a module is selected
    private async Task HandleInteraction(SocketInteraction interaction)
    {
        if (interaction is SocketMessageComponent messageComponent)
        {
            if (messageComponent.Data.CustomId == "module_selector")
            {
                var selectedModule = messageComponent.Data.Values.First();
                var embed = CreateModuleHelpEmbed(selectedModule);
                
                await messageComponent.RespondAsync(embed: embed, ephemeral: true); // Sends a private message
            }
        }
    }

    private Embed CreateModuleHelpEmbed(string moduleName)
    {
        var builder = new EmbedBuilder()
        {
            Color = new Color(114, 137, 218),
            Title = $"{moduleName.Replace("Module", "")} commands",
            Description = "Here are the commands available in this module:"
        };

        var module = commands.Modules.FirstOrDefault(m => m.Name == moduleName);
        if (module != null)
        {
            foreach (var cmd in module.Commands)
            {
                var commandDescription = cmd.Summary ?? "No description available.";
                var commandUsage = cmd.Parameters.Count > 0 && cmd.Parameters.Any(p => p.Name != "_") ? 
                    $"Usage: `{cmd.Name} {string.Join(" ", cmd.Parameters.Select(p => $"<{p.Name}>"))}`" : 
                    $"Usage: `{cmd.Name}`";

                builder.AddField(x =>
                {
                    x.Name = cmd.Name;
                    x.Value = $"{commandDescription}\n{commandUsage}";
                    x.IsInline = false;
                });
            }
        }

        return builder.Build();
    }

    [Name("Scream")]
    [Summary("Screams a random amount of 'A's.")]
    [Command("scream")]
    [Alias("screm", "a")]
    public async Task ScreamAsync()
    {
        StringBuilder scream = new();
        Random random = new();
        int count = random.Next(10, 69);
        for (int i = 0; i < count; i++)
            scream.Append('A');

        await ReplyAsync(scream.ToString());
    }

    [Name("Echo")]
    [Summary("Echoes the input.")]
    [Command("echo")]
    [Alias("say")]
    public async Task EchoAsync([Remainder] string input = "")
    {
        if(string.IsNullOrWhiteSpace(input))
        {
            await ReplyAsync("nothing");
            return;
        }

        await ReplyAsync(input);
    }

    [Name("Server Time")]
    [Summary("Displays the current time of the server.")]
    [Command("servertime")]
    [Alias("time", "st")]
    public async Task ServerTimeAsync([Remainder] string? _ = null)
    {
        await ReplyAsync($"The current time is {DateTime.UtcNow} UTC.");
    }

    [Name("Guild Age")]
    [Summary("Displays the age of the guild.")]
    [Command("guildage")]
    public async Task GuildAgeAsync([Remainder] string? _ = null)
    {
        var guild = Context.Guild;
        string age = guild.CreatedAt.UtcDateTime.GetAccurateTimeSpan(DateTime.UtcNow);

        await ReplyAsync($"Created on {guild.CreatedAt.UtcDateTime} UTC,\n{guild.Name} is {age} old.");
    }

    [Name("Time Until")]
    [Summary("Displays the time until a specified event.")]
    [Command("timeuntil")]
    [Alias("until")]
    public async Task TimeUntilAsync([Remainder] string eventName)
    {
        DateTime now = DateTime.UtcNow;
        DateTime eventDate;
        switch (eventName.ToLower())
        {
            case "new year":
            case "new years":
            case "ny":
                eventDate = new DateTime(now.Year + 1, 1, 1);
                break;
            case "christmas":
            case "xmas":
                eventDate = new DateTime(now.Year, 12, 25);
                break;
            case "halloween":
            case "hw":
                eventDate = new DateTime(now.Year, 10, 31);
                break;
            case "cc day":
            case "ccbday":
            case "cc bday":
            case "cc birthday":
                eventDate = new DateTime(now.Year, 9, 2);
                break;
            default:
                await ReplyAsync("Invalid event name.\nValid events are: `new year`, `xmas`, `halloween`, `cc bday`");
                return;
        }

        string timeUntil = now.GetAccurateTimeSpan(eventDate);
        await ReplyAsync($"Time until {eventName}: {timeUntil}");
    }

    [Name("Coin Flip")]
    [Summary("Flips a coin, or multiple coins.")]
    [Command("coinflip")]
    [Alias("flipcoin", "flip", "coin")]
    public async Task CoinFlipAsync([Remainder] string input = "1")
    {
        if (!int.TryParse(input, out int count) || count < 1)
        {
            await ReplyAsync("Invalid input. Please provide a positive non zero integer.");
            return;
        }

        if(count > 100)
        {
            await ReplyAsync("Please provide a number less than or equal to 100.");
            return;
        }

        Random random = new();
        StringBuilder sb = new();
        int heads = 0;
        int tails = 0;
        for (int i = 0; i < count; i++)
        {
            if(random.Next(2) == 0)
            {
                heads++;
                sb.Append("Heads");
            }
            else
            {
                tails++;
                sb.Append("Tails");
            }

            if (i < count - 1)
                sb.Append(", ");
        }

        if(count > 5)
            sb.Append($"\n\nHeads: {heads}\nTails: {tails}");

        await ReplyAsync(sb.ToString());
    }

    [Name("Roll Dice")]
    [Summary("Rolls a die, or multiple dice.")]
    [Command("rolldice")]
    [Alias("roll", "dice")]
    public async Task RollDiceAsync([Remainder] string input = "1d6")
    {
        if (input.AsSpan().Count('d') != 1)
        {
            await ReplyAsync("Invalid input. Please provide input in the format of `xdy` where x is the number of dice and y is the number of sides.");
            return;
        }

        string[] parts = input.Split('d');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int count) || !int.TryParse(parts[1], out int sides) || count < 1 || sides < 2)
        {
            await ReplyAsync("Invalid input. Please provide input in the format of `xdy` where x is the number of dice and y is the number of sides.");
            return;
        }

        if (sides > 100)
        {
            await ReplyAsync("Please provide a number of sides less than or equal to 100.");
            return;
        }

        if (count > 100)
        {
            await ReplyAsync("Please provide a number of dice less than or equal to 100.");
            return;
        }

        Random random = new();
        StringBuilder sb = new();
        int total = 0;
        for (int i = 0; i < count; i++)
        {
            int roll = random.Next(1, sides + 1);
            total += roll;
            sb.Append(roll);

            if (i < count - 1)
                sb.Append(", ");
        }

        if (count > 1)
            sb.Append($"\n\nTotal: {total}");

        await ReplyAsync(sb.ToString());
    }

    [Name("Choose")]
    [Summary("Randomly chooses between multiple options.")]
    [Command("choose")]
    [Alias("pick", "select")]
    public async Task ChooseAsync([Remainder] string options)
    {
        string[] parts = options.Split('\n');
        if (parts.Length < 2)
        {
            await ReplyAsync("Please provide at least two options, one on each line.");
            return;
        }

        Random random = new();
        string choice = parts[random.Next(parts.Length)].Trim();
        await ReplyAsync($"Hmmm I choose: {choice}");
    }

    [Name("Random Number")]
    [Summary("Generates a random number between the specified range.")]
    [Command("randomnumber")]
    [Alias("randomnum", "randnum", "randnumber", "random")]
    public async Task RandomNumberAsync(int min, int max)
    {
        if (min >= max)
            (min, max) = (max, min);

        Random random = new();
        int number = random.Next(min, max + 1);
        await ReplyAsync(number.ToString());
    }

    [Name("8 Ball")]
    [Summary("Ask the magic 8 ball a question.")]
    [Command("8ball")]
    [Alias("8b")]
    public async Task EightBallAsync([Remainder] string question)
    {
        string[] responses =
        [
            "It is certain.",
            "It is decidedly so.",
            "Without a doubt.",
            "Yes - definitely.",
            "You may rely on it.",
            "As I see it, yes.",
            "Most likely.",
            "Outlook good.",
            "Yes.",
            "Signs point to yes.",
            "Reply hazy, try again.",
            "Ask again later.",
            "Better not tell you now.",
            "Cannot predict now.",
            "Concentrate and ask again.",
            "Don't count on it.",
            "My reply is no.",
            "My sources say no.",
            "Outlook not so good.",
            "Very doubtful."
        ];

        Random random = new();
        string response = responses[random.Next(responses.Length)];
        await ReplyAsync(response);
    }
}