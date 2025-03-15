﻿using ColorNamesSharp;
using ColorNamesSharp.Utility;
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Morpheus.Commands;

public class MiscModule : ModuleBase<SocketCommandContextExtended>
{
    private static readonly HttpClient httpClient = new();

    private readonly CommandService commands;
    private readonly IServiceProvider serviceProvider;
    private readonly DB dbContext;
    private readonly int HelpPageSize = 10;

    public MiscModule(DiscordSocketClient client, CommandService commands, InteractionsHandler interactionHandler, IServiceProvider serviceProvider, DB dbContext)
    {
        this.commands = commands;
        this.serviceProvider = serviceProvider;
        this.dbContext = dbContext;

        interactionHandler.RegisterInteraction("module_selector", HandleHelpSelectorInteraction);
    }

    [Name("Help")]
    [Summary("Displays a list of commands.")]
    [Command("help")]
    [Alias("commands", "cmds", "h")]
    [RateLimit(1, 30)]
    public async Task HelpAsync()
    {
        EmbedBuilder builder = new()
        {
            Color = Colors.Blue,
            Description = "Select a command module to view its commands."
        };

        // Create the selector (dropdown)
        List<string> modules = commands.Modules.Select(m => m.Name).ToList();
        SelectMenuBuilder selectMenu = new SelectMenuBuilder()
            .WithPlaceholder("Select a module")
            .WithCustomId("module_selector");

        // Add the options
        foreach (ModuleInfo? module in commands.Modules)
        {
            if (module.Commands.Count <= HelpPageSize)
                selectMenu.AddOption(new SelectMenuOptionBuilder().WithLabel(module.Name.Replace("Module", "")).WithValue("1_" + module.Name));
            else
            {
                int j = 1;
                for (int i = 0; i < module.Commands.Count; i += HelpPageSize)
                {
                    selectMenu.AddOption(new SelectMenuOptionBuilder().WithLabel(module.Name.Replace("Module", "") + " " + j).WithValue($"{j}_{module.Name}"));
                    j++;
                }
            }
        }

        // Create an interaction message
        MessageComponent component = new ComponentBuilder().WithSelectMenu(selectMenu).Build();

        // Create the initial embed
        IUserMessage message = await ReplyAsync(embed: builder.Build());

        await message.ModifyAsync(msg => msg.Components = component);
    }

    // Handle the interaction when a module is selected
    private async Task HandleHelpSelectorInteraction(SocketInteraction interaction)
    {
        if (interaction is SocketMessageComponent messageComponent)
        {
            if (messageComponent.Data.CustomId == "module_selector")
            {
                Guild? guild = await dbContext.Guilds.FirstOrDefaultAsync(g => g.DiscordId == interaction.GuildId);
                string commandPrefix = guild?.Prefix ?? Env.Variables["BOT_DEFAULT_COMMAND_PREFIX"];

                string selectedModule = messageComponent.Data.Values.First();
                Embed embed = CreateModuleHelpEmbed(selectedModule, commandPrefix);

                // Fetch the original message using message ID
                IUserMessage? message = await messageComponent.Channel.GetMessageAsync(messageComponent.Message.Id) as IUserMessage;

                if (message != null)
                {
                    await message.ModifyAsync(prop => prop.Embed = embed); // Sends a private message
                    await messageComponent.DeferAsync();
                }
            }
        }
    }

    private Embed CreateModuleHelpEmbed(string moduleName, string commandPrefix)
    {
        int page = int.Parse(moduleName.Split("_")[0]);
        string name = moduleName.Split("_")[1].Replace("Module", "");

        EmbedBuilder builder = new()
        {
            Color = Colors.Blue,
            Title = $"{name} {page} commands",
            Description = "Here are the commands available in this module:"
        };

        ModuleInfo? module = commands.Modules.FirstOrDefault(m => m.Name == name + "Module");

        if (module != null)
        {
            for (int i = (page - 1) * HelpPageSize; i < page * HelpPageSize && i < module.Commands.Count; i++)
            {
                CommandInfo cmd = module.Commands.ElementAt(i);

                string aliases = cmd.Aliases.Count > 1
                    ? $"Aliases: {string.Join(", ", cmd.Aliases.Skip(1).Select(a => commandPrefix + a))}"
                    : "No aliases available.";

                string commandDescription = cmd.Summary ?? "No description available.";

                string commandUsage = cmd.Parameters.Count > 0 && cmd.Parameters.Any(p => p.Name != "_")
                    ? $"Usage: `{commandPrefix}{cmd.Aliases[0]} {string.Join(" ", cmd.Parameters.Select(p => $"[{p.Name}{(p.IsOptional ? "?" : "")}{(!string.IsNullOrEmpty(p.DefaultValue?.ToString()) ? " = " + p.DefaultValue.ToString() : "")}]"))}`"
                    : $"Usage: `{commandPrefix}{cmd.Aliases[0]}`";

                builder.AddField(x =>
                {
                    x.Name = cmd.Name;
                    x.Value = $"{commandDescription}\n{commandUsage}\n{aliases}";
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
    [RateLimit(3, 10)]
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
    [RateLimit(3, 10)]
    public async Task EchoAsync([Remainder] string input = "")
    {
        if (string.IsNullOrWhiteSpace(input))
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
    [RateLimit(3, 10)]
    public async Task ServerTimeAsync([Remainder] string? _ = null)
    {
        await ReplyAsync($"The current time is {DateTime.UtcNow} UTC.");
    }

    [Name("Guild Age")]
    [Summary("Displays the age of the guild.")]
    [Command("guildage")]
    [RateLimit(3, 10)]
    public async Task GuildAgeAsync([Remainder] string? _ = null)
    {
        SocketGuild guild = Context.Guild;
        string age = guild.CreatedAt.UtcDateTime.GetAccurateTimeSpan(DateTime.UtcNow);

        await ReplyAsync($"Created on {guild.CreatedAt.UtcDateTime} UTC,\n{guild.Name} is {age} old.");
    }

    [Name("Time Until")]
    [Summary("Displays the time until a specified event.")]
    [Command("timeuntil")]
    [Alias("until")]
    [RateLimit(3, 10)]
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
    [RateLimit(3, 10)]
    public async Task CoinFlipAsync([Remainder] string input = "1")
    {
        if (!int.TryParse(input, out int count) || count < 1)
        {
            await ReplyAsync("Invalid input. Please provide a positive non zero integer.");
            return;
        }

        if (count > 100)
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
            if (random.Next(2) == 0)
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

        if (count > 5)
            sb.Append($"\n\nHeads: {heads}\nTails: {tails}");

        await ReplyAsync(sb.ToString());
    }

    [Name("Roll Dice")]
    [Summary("Rolls a die, or multiple dice.")]
    [Command("rolldice")]
    [Alias("roll", "dice")]
    [RateLimit(3, 10)]
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
    [RateLimit(3, 10)]
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
    [RateLimit(3, 10)]
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
    [RateLimit(3, 10)]
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

    [Name("Rock Paper Scissors")]
    [Summary("Play a game of rock, paper, scissors.")]
    [Command("rps")]
    [Alias("rockpaperscissors")]
    [RateLimit(3, 10)]
    public async Task RockPaperScissorsAsync(string choice)
    {
        string[] choices = ["rock", "paper", "scissors"];
        if (!choices.Contains(choice.ToLower()))
        {
            await ReplyAsync("Invalid choice. Please choose either `rock`, `paper`, or `scissors`.");
            return;
        }
        Random random = new();
        string botChoice = choices[random.Next(choices.Length)];
        
        if(choice == botChoice)
        {
            await ReplyAsync($"It's a tie! I chose {botChoice} too.");
            return;
        }
        
        if((choice == "rock" && botChoice == "scissors") || (choice == "paper" && botChoice == "rock") || (choice == "scissors" && botChoice == "paper"))
        {
            await ReplyAsync($"You win! I chose {botChoice}.");
            return;
        }
     
        if((choice == "rock" && botChoice == "paper") || (choice == "paper" && botChoice == "scissors") || (choice == "scissors" && botChoice == "rock"))
        {
            await ReplyAsync($"You lose! I chose {botChoice}.");
            return;
        }
    }

    [Name("Info")]
    [Summary("Displays information about the bot.")]
    [Command("info")]
    [Alias("information", "about", "botinfo")]
    [RateLimit(3, 10)]
    public async Task InfoAsync()
    {
        ulong ownerId = ulong.Parse(Env.Variables["OWNER_ID"]);

        EmbedBuilder builder = new()
        {
            Color = Colors.Blue,
            Title = "Morpheus",
            Description = $"""
            A multi-purpose Discord bot written in C# using Discord.Net.
            **Warning:** Might contain traces of sentince and Codify!

            **Links:**
            [Website]({Env.Variables["WEBSITE"]})
            [GitHub Repository]({Env.Variables["GITHUB_REPO_URL"]}) 
            [Invite Link]({Env.Variables["BOT_INVITE_URL"]})
            [Support Discord Server]({Env.Variables["BOT_SUPPORT_SERVER_URL"]})

            -# Version: v{Utils.GetAssemblyVersion()}
            """,
            ThumbnailUrl = Context.Client.CurrentUser.GetAvatarUrl(),
            Footer = new EmbedFooterBuilder()
            {
                Text = "Made with ❤️ by VycDev",
                IconUrl = (await Context.Client.Rest.GetUserAsync(ownerId)).GetAvatarUrl()
            }
        };

        await ReplyAsync(embed: builder.Build());
    }

    [Name("Uptime")]
    [Summary("Displays the bot's uptime.")]
    [Command("uptime")]
    [RateLimit(3, 10)]
    public async Task UptimeAsync()
    {
        string uptime = Env.StartTime.GetAccurateTimeSpan(DateTime.UtcNow);
        await ReplyAsync($"Bot turned on {uptime} ago.");
    }

    [Name("Random Color")]
    [Summary("Generates a random color.")]
    [Command("randomcolor")]
    [Alias("randcolor", "rc")]
    [RateLimit(2, 10)]
    public async Task RandomColorAsync()
    {
        NamedColor? color = Colors.ColorNames.GetRandomNamedColor();
        if (color is null)
        {
            await ReplyAsync("Failed to generate a random color.");
            return;
        }

        EmbedBuilder embed = new()
        {
            Color = new(color.Rgb.Item1, color.Rgb.Item2, color.Rgb.Item3),
            Title = color.Name,
            Description =
            $"""
            HEX: {color.Hex}
            RGB: {color.Rgb.Item1}, {color.Rgb.Item2}, {color.Rgb.Item3}
            LAB: {color.Lab.Item1:F2}, {color.Lab.Item2:F2}, {color.Lab.Item3:F2}
            """
        };

        await ReplyAsync(embed: embed.Build());
    }

    [Name("Find color name")]
    [Summary("Finds the closest color name match, based on a hex value")]
    [Command("findcolor")]
    [Alias("findc", "fc")]
    [RateLimit(2, 10)]
    public async Task FindColorName([Remainder] string hexValue)
    {
        try
        {
            (short r, short g, short b) Rgb = ColorConverter.HexToRgb(hexValue);
            NamedColor? color = Colors.ColorNames.FindClosestColor(Rgb);

            if (color == null)
            {
                await ReplyAsync("Color name couldn't be found");
                return;
            }

            EmbedBuilder embed = new()
            {
                Color = new(color.Rgb.Item1, color.Rgb.Item2, color.Rgb.Item3),
                Title = color.Name,
                Description =
                $"""
                HEX: {color.Hex}
                RGB: {color.Rgb.Item1}, {color.Rgb.Item2}, {color.Rgb.Item3}
                LAB: {color.Lab.Item1:F2}, {color.Lab.Item2:F2}, {color.Lab.Item3:F2}
                """
            };

            await ReplyAsync(embed: embed.Build());

        }
        catch (ArgumentException ex)
        {
            await ReplyAsync(ex.Message);
        }
    }

    [Name("How gay")]
    [Summary("Determines how gay a person is based on their nickname")]
    [Command("howgay")]
    [RateLimit(3, 10)]
    public async Task HowGayAsync(SocketGuildUser? user = null)
    {
        user ??= Context.User as SocketGuildUser;

        Random random = new(user.Nickname.Select(c => (int)c).Sum());
        int gayness = random.Next(101);

        EmbedBuilder embed = new()
        {
            Color = Colors.Blue,
            Title = $":rainbow_flag: {user.Username} is {gayness}% gay",
            Description = $"{gayness.GetPercentageBar()}",
            Footer = new EmbedFooterBuilder()
            {
                Text = "Gayness is a spectrum.",
                IconUrl = user.GetAvatarUrl()
            }
        };

        await ReplyAsync(embed: embed.Build());
    }

    [Name("Year Percentage")]
    [Summary("Displays the percentage of the year that has passed.")]
    [Command("yearpercentage")]
    [Alias("yearpercent", "yp")]
    [RateLimit(3, 10)]
    public async Task YearPercentageAsync()
    {
        DateTime now = DateTime.UtcNow;
        DateTime startOfYear = new(now.Year, 1, 1);
        DateTime endOfYear = new(now.Year + 1, 1, 1);

        double percentage = ((now - startOfYear).TotalDays / (endOfYear - startOfYear).TotalDays * 100);

        EmbedBuilder embed = new()
        {
            Color = Colors.Blue,
            Title = $"{now.Year} is {percentage:F2}% done",
            Description = $"{((int)percentage).GetPercentageBar()}",
        };

        await ReplyAsync(embed: embed.Build());
    }

    [Name("Ping")]
    [Summary("Displays the latency between the bot and discord.")]
    [Command("ping")]
    [RateLimit(3, 10)]
    public async Task PingAsync()
    {
        await ReplyAsync($"Pong! {Context.Client.Latency}ms");
    }

    [Name("Urban Dictionary")]
    [Summary("Returns definitions from urban dictionary")]
    [Command("udic")]
    [Alias("urbandictionary", "urbandic", "udictionary")]
    [RateLimit(5, 30)]
    public async Task UrbanDictionary(string? word = null)
    {
        string url;
        
        if(!string.IsNullOrWhiteSpace(word))
            url = $"https://api.urbandictionary.com/v0/define?term={word}";
        else
            url = "https://api.urbandictionary.com/v0/random";

        HttpResponseMessage response = await httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            await ReplyAsync("Failed to fetch the definition. Try again later.");
            return;
        }

        string json = await response.Content.ReadAsStringAsync();
        JObject data = JObject.Parse(json);
        JArray list = (JArray)data["list"];

        if (list.Count == 0)
        {
            await ReplyAsync($"No definition found for **{word}**.");
            return;
        }

        string definition = list[0]["definition"].ToString();
        string example = list[0]["example"].ToString();
        string permalink = list[0]["permalink"].ToString();

        var embed = new EmbedBuilder()
            .WithTitle($"Urban Dictionary: **{word}**")
            .WithUrl(permalink)
            .WithDescription(definition.Length > 1024 ? definition.Substring(0, 1021) + "..." : definition)
            .AddField("Example", string.IsNullOrWhiteSpace(example) ? "N/A" : example.Length > 1024 ? example.Substring(0, 1021) + "..." : example, false)
            .WithColor(Color.Blue)
            .WithFooter("Powered by Urban Dictionary");

        await ReplyAsync(embed: embed.Build());
    }

    [Name("Pin")]
    [Summary("Pins a message.")]
    [Command("pin")]
    [RateLimit(5, 30)]
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

    [Name("Ping Minecraft Server")]
    [Summary("Pings a Minecraft server to get information about it.")]
    [Command("pingmc")]
    [Alias("mcserver", "mcstatus")]
    [RateLimit(2, 30)]
    public async Task PingMinecraftServerAsync(string ip, int port = 25565)
    {
        var message = await ReplyAsync("Fetching data...");

        string url = $"https://api.mcsrvstat.us/2/{ip}:{port}";

        // Create an HttpClient and set the User-Agent header
        using (var httpClient = new HttpClient())
        {
            httpClient.DefaultRequestHeaders.Add("User-Agent", $"{Env.Variables["BOT_NAME"]}/{Utils.GetAssemblyVersion()}");

            HttpResponseMessage response = await httpClient.GetAsync(url);
            string content = await response.Content.ReadAsStringAsync();

            JObject data = JObject.Parse(content);

            // Check if server is online
            if (data["online"] == null || !data["online"].Value<bool>())
            {
                var offlineEmbed = new EmbedBuilder()
                    .WithColor(Color.Orange)
                    .WithTitle($"{ip}:{port} is Offline")
                    .WithDescription("The server appears to be offline.")
                    .WithFooter("Powered by mcsrvstat.us")
                    .WithCurrentTimestamp()
                    .Build();

                await message.ModifyAsync(msg =>
                {
                    msg.Content = string.Empty;
                    msg.Embed = offlineEmbed;
                });
                
                return;
            }

            // Extract data if online
            string version = data["version"]?.ToString() ?? "N/A";
            var motdArray = data["motd"]?["clean"] as JArray;
            string motd = motdArray != null ? string.Join("\n", motdArray) : "N/A";
            int playersOnline = data["players"]?["online"]?.Value<int>() ?? 0;
            int maxPlayers = data["players"]?["max"]?.Value<int>() ?? 0;
            string software = data["software"]?.ToString() ?? "Unknown";
            string serverImageBase64 = data["icon"]?.ToString(); // Base64 encoded image

            // Build a nicely formatted embed
            var embed = new EmbedBuilder()
                .WithColor(Color.Green)
                .WithTitle("Minecraft Server Status")
                .WithDescription($"**Server:** {ip}:{port}")
                .AddField("Version", version, inline: true)
                .AddField("Players", $"{playersOnline}/{maxPlayers}", inline: true)
                .AddField("Software", software, inline: true)
                .AddField("MOTD", motd)
                .WithFooter("Powered by mcsrvstat.us")
                .WithCurrentTimestamp();

            // Include image if the favicon exists
            if (!string.IsNullOrEmpty(serverImageBase64))
            {
                embed.WithThumbnailUrl($"attachment://favicon.png"); // Point to an inline attachment URL
                var imageBytes = Convert.FromBase64String(serverImageBase64.Split(',')[1]); // Remove the data:image/png;base64, part
                var stream = new MemoryStream(imageBytes);
                var attachment = new FileAttachment(stream, "favicon.png");

                var finalEmbed = embed.Build();
                // await Context.Channel.SendFileAsync(attachment: attachment, embed: finalEmbed);
                await message.ModifyAsync(msg =>
                {
                    msg.Content = string.Empty;
                    msg.Embed = finalEmbed;
                    msg.Attachments = new([attachment]);
                });
            }
            else
            {
                var finalEmbed = embed.Build();

                await message.ModifyAsync(msg =>
                {
                    msg.Content = string.Empty;
                    msg.Embed = finalEmbed;
                });
            }
        }
    }

    [Name("Hash")]
    [Summary("Hashes a string using the specified algorithm.")]
    [Command("hash")]
    [Alias("hashstring")]
    [RateLimit(3, 10)]
    public async Task HashAsync(string algorithm, [Remainder] string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            await ReplyAsync("Please provide a string to hash.");
            return;
        }
        
        HashAlgorithm? hashAlgorithm = algorithm.ToLower() switch
        {
            "md5" => MD5.Create(),
            "sha1" => SHA1.Create(),
            "sha256" => SHA256.Create(),
            "sha384" => SHA384.Create(),
            "sha512" => SHA512.Create(),
            _ => null
        };

        if (hashAlgorithm == null)
        {
            await ReplyAsync("Invalid algorithm. Supported algorithms are: `md5`, `sha1`, `sha256`, `sha384`, `sha512`.");
            return;
        }
        
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = hashAlgorithm.ComputeHash(inputBytes);
        string hash = BitConverter.ToString(hashBytes).Replace("-", "");
        await ReplyAsync($"Hashed using {algorithm.ToUpper()}: `{hash}`");
    }

    [Name("Timer")]
    [Summary("Sets a timer for the specified duration.")]
    [Command("timer")]
    [Alias("settimer")]
    [RateLimit(3, 10)]
    public async Task TimerAsync(int duration, string format = "seconds")
    {
        if (duration < 1)
        {
            await ReplyAsync("Please provide a duration greater than 0.");
            return;
        }

        if (format != "seconds" && format != "minutes" && format != "hours")
        {
            await ReplyAsync("Invalid format. Supported formats are: `seconds`, `minutes`, `hours`.");
            return;
        }

        int durationSeconds = duration;

        if (format == "minutes")
            durationSeconds *= 60;
        else if (format == "hours")
            durationSeconds *= 3600;

        if (durationSeconds > 3600 * 24)
        {
            await ReplyAsync($"Please provide a duration less than or equal to 24 hours.");
            return;
        }

        await ReplyAsync($"Timer set for {duration} {format}.");
        await Task.Delay(duration * 1000);
        await ReplyAsync($"{Context.User.Mention} {duration} {format} have passed.");
    }
}
