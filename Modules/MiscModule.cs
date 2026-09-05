using ColorNamesSharp;
using ColorNamesSharp.Utility;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Extensions;
using Morpheus.Handlers;
using Morpheus.Utilities;
using Morpheus.Utilities.Extensions;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Morpheus.Modules;

public class MiscModule(CommandService commands, IServiceProvider serviceProvider, DB dbContext) : MorpheusModuleBase
{
    private const int DiscordMessageMaxLength = 2000;
    private const string ChoiceReplyPrefix = "Hmmm I choose: ";
    private static readonly int MaxChoiceLength = DiscordMessageMaxLength - ChoiceReplyPrefix.Length;
    private static readonly HttpClient httpClient = new();

    private readonly CommandService commands = commands;
    private readonly IServiceProvider serviceProvider = serviceProvider;
    private readonly DB dbContext = dbContext;

    [Name("Scream")]
    [Summary("Screams a random amount of 'A's.")]
    [Command("scream")]
    [Alias("screm", "a")]
    [RateLimit(3, 10)]
    public async Task Scream()
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
    public async Task Echo([Remainder] string input = "")
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
    public async Task ServerTime([Remainder] string? _ = null)
    {
        await ReplyAsync($"The current time is {DateTime.UtcNow} UTC.");
    }

    [Name("Guild Age")]
    [Summary("Displays the age of the guild.")]
    [Command("guildage")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 10)]
    public async Task GuildAge([Remainder] string? _ = null)
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
    public async Task TimeUntil([Remainder] string eventName)
    {
        DateTime now = DateTime.UtcNow;
        DateTime eventDate;
        switch (NormalizeTimeUntilEventName(eventName))
        {
            case "new year":
            case "new years":
            case "ny":
                eventDate = new DateTime(now.Year + 1, 1, 1);
                break;
            case "christmas":
            case "xmas":
                eventDate = GetNextAnnualEventDate(now, 12, 25);
                break;
            case "halloween":
            case "hw":
                eventDate = GetNextAnnualEventDate(now, 10, 31);
                break;
            case "cc day":
            case "ccbday":
            case "cc bday":
            case "cc birthday":
                eventDate = GetNextAnnualEventDate(now, 9, 2);
                break;
            default:
                await ReplyAsync("Invalid event name.\nValid events are: `new year`, `xmas`, `halloween`, `cc bday`");
                return;
        }

        string timeUntil = now.GetAccurateTimeSpan(eventDate);
        await ReplyAsync($"Time until {eventName}: {timeUntil}");
    }

    internal static DateTime GetNextAnnualEventDate(DateTime now, int month, int day)
    {
        DateTime eventDate = new(now.Year, month, day, 0, 0, 0, now.Kind);
        return eventDate < now ? eventDate.AddYears(1) : eventDate;
    }

    internal static string NormalizeTimeUntilEventName(string eventName) => eventName.Trim().ToLowerInvariant();

    [Name("Coin Flip")]
    [Summary("Flips a coin, or multiple coins.")]
    [Command("coinflip")]
    [Alias("flipcoin", "flip", "coin")]
    [RateLimit(3, 10)]
    public async Task CoinFlip([Remainder] string input = "1")
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
    public async Task RollDice([Remainder] string input = "1d6")
    {
        if (!TryParseDiceInput(input, out int count, out int sides))
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

    internal static bool TryParseDiceInput(string input, out int count, out int sides)
    {
        ReadOnlySpan<char> inputSpan = input.AsSpan();
        int separatorIndex = inputSpan.IndexOfAny('d', 'D');

        count = 0;
        sides = 0;

        return separatorIndex >= 0
            && inputSpan[(separatorIndex + 1)..].IndexOfAny('d', 'D') < 0
            && int.TryParse(inputSpan[..separatorIndex], out count)
            && int.TryParse(inputSpan[(separatorIndex + 1)..], out sides)
            && count >= 1
            && sides >= 2;
    }

    [Name("Choose")]
    [Summary("Randomly chooses between multiple options.")]
    [Command("choose")]
    [Alias("pick", "select")]
    [RateLimit(3, 10)]
    public async Task Choose([Remainder] string options)
    {
        string[] parts = ParseChoices(options);
        if (parts.Length < 2)
        {
            await ReplyAsync("Please provide at least two options, one on each line.");
            return;
        }

        Random random = new();
        string choice = parts[random.Next(parts.Length)];
        await ReplyAsync(ChoiceReplyPrefix + choice);
    }

    internal static string[] ParseChoices(string options)
    {
        return options.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(TruncateChoice)
            .ToArray();
    }

    private static string TruncateChoice(string choice)
    {
        if (choice.Length <= MaxChoiceLength)
            return choice;

        int prefixLength = MaxChoiceLength - 1;
        if (char.IsHighSurrogate(choice[prefixLength - 1]) && char.IsLowSurrogate(choice[prefixLength]))
            prefixLength--;

        return string.Concat(choice.AsSpan(0, prefixLength), "…");
    }

    [Name("Random Number")]
    [Summary("Generates a random number between the specified range.")]
    [Command("randomnumber")]
    [Alias("randomnum", "randnum", "randnumber", "random")]
    [RateLimit(3, 10)]
    public async Task RandomNumber(int min, int max)
    {
        int number = GenerateRandomNumber(min, max);
        await ReplyAsync(number.ToString());
    }

    internal static int GenerateRandomNumber(int min, int max)
    {
        if (min > max)
            (min, max) = (max, min);

        return (int)Random.Shared.NextInt64(min, (long)max + 1);
    }

    [Name("8 Ball")]
    [Summary("Ask the magic 8 ball a question.")]
    [Command("8ball")]
    [Alias("8b")]
    [RateLimit(3, 10)]
    public async Task EightBall([Remainder] string question)
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
    public async Task RockPaperScissors(string choice)
    {
        string[] choices = ["rock", "paper", "scissors"];
        choice = NormalizeRockPaperScissorsChoice(choice);
        if (!choices.Contains(choice))
        {
            await ReplyAsync("Invalid choice. Please choose either `rock`, `paper`, or `scissors`.");
            return;
        }
        Random random = new();
        string botChoice = choices[random.Next(choices.Length)];

        if (choice == botChoice)
        {
            await ReplyAsync($"It's a tie! I chose {botChoice} too.");
            return;
        }

        if ((choice == "rock" && botChoice == "scissors") || (choice == "paper" && botChoice == "rock") || (choice == "scissors" && botChoice == "paper"))
        {
            await ReplyAsync($"You win! I chose {botChoice}.");
            return;
        }

        if ((choice == "rock" && botChoice == "paper") || (choice == "paper" && botChoice == "scissors") || (choice == "scissors" && botChoice == "rock"))
        {
            await ReplyAsync($"You lose! I chose {botChoice}.");
            return;
        }
    }

    internal static string NormalizeRockPaperScissorsChoice(string choice) => choice.Trim().ToLowerInvariant();

    [Name("Info")]
    [Summary("Displays information about the bot.")]
    [Command("info")]
    [Alias("information", "about", "botinfo")]
    [RateLimit(3, 10)]
    public async Task Info()
    {
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
            Footer = BuildInfoFooter()
        };

        await ReplyAsync(embed: builder.Build());
    }

    internal static EmbedFooterBuilder BuildInfoFooter() => new()
    {
        Text = "Made with ❤️ by vycdev"
    };

    [Name("Uptime")]
    [Summary("Displays the bot's uptime.")]
    [Command("uptime")]
    [RateLimit(3, 10)]
    public async Task Uptime()
    {
        string uptime = Env.StartTime.GetAccurateTimeSpan(DateTime.UtcNow);
        await ReplyAsync($"Bot turned on {uptime} ago.");
    }

    [Name("Random Color")]
    [Summary("Generates a random color.")]
    [Command("randomcolor")]
    [Alias("randcolor", "rc")]
    [RateLimit(2, 10)]
    public async Task RandomColor()
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
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 10)]
    public async Task HowGay(SocketGuildUser? user = null)
    {
        user ??= Context.User as SocketGuildUser;
        if (user == null)
        {
            await ReplyAsync("User not found.");
            return;
        }

        string name = user.Nickname ?? user.Username;

        Random random = new(name.Select(c => (int)c).Sum());
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
    public async Task YearPercentage()
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
    public async Task Ping()
    {
        await ReplyAsync($"Pong! {Context.Client.Latency}ms");
    }

    [Name("Urban Dictionary")]
    [Summary("Returns definitions from urban dictionary")]
    [Command("udic")]
    [Alias("urbandictionary", "urbandic", "udictionary")]
    [RateLimit(5, 30)]
    public async Task UrbanDictionary([Remainder] string? word = null)
    {
        string url = BuildUrbanDictionaryUrl(word);

        HttpResponseMessage response = await httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            await ReplyAsync("Failed to fetch the definition. Try again later.");
            return;
        }

        string json = await response.Content.ReadAsStringAsync();
        JObject data = JObject.Parse(json);
        JArray? list = data["list"] as JArray;

        if (list == null || list.Count == 0)
        {
            await ReplyAsync($"No definition found for **{word}**.");
            return;
        }

        JObject? entry = list[0] as JObject;
        if (entry == null)
        {
            await ReplyAsync("Urban Dictionary returned an unexpected response. Try again later.");
            return;
        }

        string definition = entry.Value<string>("definition") ?? "No definition found.";
        string example = entry.Value<string>("example") ?? "";
        string permalink = entry.Value<string>("permalink") ?? "https://www.urbandictionary.com/";

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle($"Urban Dictionary: **{word ?? "Random"}**")
            .WithUrl(permalink)
            .WithDescription(TruncateUrbanDictionaryText(definition))
            .AddField("Example", string.IsNullOrWhiteSpace(example) ? "N/A" : TruncateUrbanDictionaryText(example), false)
            .WithColor(Color.Blue)
            .WithFooter("Powered by Urban Dictionary");

        await ReplyAsync(embed: embed.Build());
    }

    internal static string BuildUrbanDictionaryUrl(string? word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return "https://api.urbandictionary.com/v0/random";

        return $"https://api.urbandictionary.com/v0/define?term={Uri.EscapeDataString(word)}";
    }

    internal static string TruncateUrbanDictionaryText(string value)
    {
        if (value.Length <= EmbedFieldBuilder.MaxFieldValueLength)
            return value;

        int contentLength = EmbedFieldBuilder.MaxFieldValueLength - 3;
        if (char.IsHighSurrogate(value[contentLength - 1]) && char.IsLowSurrogate(value[contentLength]))
            contentLength--;

        return value[..contentLength] + "...";
    }

    [Name("Ping Minecraft Server")]
    [Summary("Pings a Minecraft server to get information about it.")]
    [Command("pingmc")]
    [Alias("mcserver", "mcstatus")]
    [RequireBotPermission(GuildPermission.AttachFiles)]
    [RateLimit(2, 30)]
    public async Task PingMinecraftServer(string ip, int port = 25565)
    {
        IUserMessage message = await ReplyAsync("Fetching data...");

        string url = $"https://api.mcsrvstat.us/2/{ip}:{port}";

        // Create an HttpClient and set the User-Agent header
        using HttpClient httpClient = new();
        httpClient.DefaultRequestHeaders.Add("User-Agent", $"{Env.Variables["BOT_NAME"]}/{Utils.GetAssemblyVersion()}");

        HttpResponseMessage response = await httpClient.GetAsync(url);
        string content = await response.Content.ReadAsStringAsync();

        JObject data = JObject.Parse(content);

        // Check if server is online
        if (data.Value<bool?>("online") != true)
        {
            Embed offlineEmbed = new EmbedBuilder()
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
        string motd = data["motd"]?["clean"] is JArray motdArray ? string.Join("\n", motdArray) : "N/A";
        int playersOnline = data["players"]?["online"]?.Value<int>() ?? 0;
        int maxPlayers = data["players"]?["max"]?.Value<int>() ?? 0;
        string software = data["software"]?.ToString() ?? "Unknown";
        string? serverImageBase64 = data["icon"]?.ToString(); // Base64 encoded image

        // Build a nicely formatted embed
        EmbedBuilder embed = new EmbedBuilder()
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
        if (TryDecodeMinecraftFavicon(serverImageBase64, out byte[] imageBytes))
        {
            embed.WithThumbnailUrl($"attachment://favicon.png"); // Point to an inline attachment URL
            MemoryStream stream = new(imageBytes);
            FileAttachment attachment = new(stream, "favicon.png");

            Embed finalEmbed = embed.Build();
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
            Embed finalEmbed = embed.Build();

            await message.ModifyAsync(msg =>
            {
                msg.Content = string.Empty;
                msg.Embed = finalEmbed;
            });
        }
    }

    internal static bool TryDecodeMinecraftFavicon(string? favicon, out byte[] imageBytes)
    {
        imageBytes = [];
        if (string.IsNullOrWhiteSpace(favicon))
            return false;

        const string base64Marker = ";base64,";
        int markerIndex = favicon.IndexOf(base64Marker, StringComparison.OrdinalIgnoreCase);
        if (favicon.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && markerIndex < 0)
            return false;

        string encodedImage = markerIndex >= 0
            ? favicon[(markerIndex + base64Marker.Length)..]
            : favicon;

        try
        {
            imageBytes = Convert.FromBase64String(encodedImage);
            return imageBytes.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    [Name("Hash")]
    [Summary("Hashes a string using the specified algorithm.")]
    [Command("hash")]
    [Alias("hashstring")]
    [RateLimit(3, 10)]
    public async Task Hash(string algorithm, [Remainder] string input)
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

    [Name("Love Compatibility")]
    [Summary("Calculates the love compatibility between two people.")]
    [Command("love")]
    [Alias("lovecompatibility", "lovecalc", "lovecouple")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 10)]
    public async Task LoveCompatibility(SocketGuildUser user1, SocketGuildUser? user2 = null)
    {
        user2 ??= Context.User as SocketGuildUser;
        if (user2 == null)
        {
            await ReplyAsync("User not found.");
            return;
        }

        string name1 = user1.Nickname ?? user1.Username;
        string name2 = user2.Nickname ?? user2.Username;

        int seed = name1.Select(c => (int)c).Sum() + name2.Select(c => (int)c).Sum();
        Random random = new(seed);
        int lovePercentage = random.Next(101);
        EmbedBuilder embed = new EmbedBuilder()
            .WithColor(Color.Blue)
            .WithTitle($":heart: {user1.Username} and {user2.Username} are {lovePercentage}% compatible")
            .WithDescription($"{lovePercentage.GetPercentageBar()}")
            .WithFooter("Love is in the air")
            .WithCurrentTimestamp();
        await ReplyAsync(embed: embed.Build());
    }

    [Name("Get Profile Picture")]
    [Summary("Gets the profile picture of a user.")]
    [Command("profilepic")]
    [Alias("avatar", "pfp")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 10)]
    public async Task GetProfilePicture(SocketGuildUser? user = null)
    {
        user ??= Context.User as SocketGuildUser;
        if (user == null)
        {
            await ReplyAsync("User not found.");
            return;
        }
        string avatarUrl = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl();
        EmbedBuilder embed = new EmbedBuilder()
            .WithColor(Color.Blue)
            .WithTitle($"{user.Username}'s Profile Picture")
            .WithImageUrl(avatarUrl)
            .WithFooter("Profile picture fetched successfully")
            .WithCurrentTimestamp();
        await ReplyAsync(embed: embed.Build());
    }

    [Name("Get Bot Invite Link")]
    [Summary("Gets the bot's invite link with the required permissions.")]
    [Command("botinvite")]
    [Alias("invitebot", "getinvite")]
    [RateLimit(3, 10)]
    public async Task GetBotInviteLink()
    {
        string inviteUrl = Env.Variables["BOT_INVITE_URL"];
        if (string.IsNullOrWhiteSpace(inviteUrl))
        {
            await ReplyAsync("Bot invite link is not configured.");
            return;
        }
        EmbedBuilder embed = new EmbedBuilder()
            .WithColor(Color.Blue)
            .WithTitle("Bot Invite Link")
            .WithDescription($"Click [here]({inviteUrl}) to invite the bot to your server.")
            .WithFooter("Invite link fetched successfully")
            .WithCurrentTimestamp();

        await ReplyAsync(embed: embed.Build());
    }


    [Name("Get User Info")]
    [Summary("Gets information about a user.")]
    [Command("userinfo")]
    [Alias("user", "whois")]
    [RequireContext(ContextType.Guild)]
    [RateLimit(3, 10)]
    public async Task UserInfo(SocketGuildUser? user = null)
    {
        user ??= Context.User as SocketGuildUser;
        if (user == null)
        {
            await ReplyAsync("User not found.");
            return;
        }
        EmbedBuilder embed = new()
        {
            Color = Colors.Blue,
            Title = $"{user.Username}'s Info",
            ThumbnailUrl = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl(),
            Description = $"ID: {user.Id}\nJoined At: {user.JoinedAt?.UtcDateTime}\nCreated At: {user.CreatedAt.UtcDateTime}",
            Fields =
            {
                new EmbedFieldBuilder().WithName("Roles").WithValue(FormatUserRoles(user.Roles.Select(r => r.Name))),
                new EmbedFieldBuilder().WithName("Status").WithValue(user.Status.ToString()),
                new EmbedFieldBuilder().WithName("Is Bot").WithValue(user.IsBot ? "Yes" : "No"),
            },
            Footer = new EmbedFooterBuilder()
            {
                Text = "User Info",
                IconUrl = user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl()
            }
        };
        await ReplyAsync(embed: embed.Build());
    }

    internal static string FormatUserRoles(IEnumerable<string> roleNames)
    {
        string[] roles = roleNames.Where(roleName => !string.IsNullOrWhiteSpace(roleName)).ToArray();
        if (roles.Length == 0)
            return "None";

        StringBuilder result = new();
        for (int i = 0; i < roles.Length; i++)
        {
            string separator = result.Length == 0 ? string.Empty : ", ";
            int omittedCount = roles.Length - i - 1;
            string omissionSuffix = omittedCount > 0 ? $", … (+{omittedCount} more)" : string.Empty;
            if (result.Length + separator.Length + roles[i].Length + omissionSuffix.Length > EmbedFieldBuilder.MaxFieldValueLength)
            {
                omissionSuffix = $", … (+{roles.Length - i} more)";
                return result.Append(omissionSuffix).ToString();
            }

            result.Append(separator).Append(roles[i]);
        }

        return result.ToString();
    }

    [Name("Random Advice")]
    [Summary("Fetches a random piece of advice.")]
    [Command("advice")]
    [Alias("getadvice", "randomadvice")]
    [RateLimit(3, 10)]
    public async Task RandomAdvice()
    {
        string url = "https://api.adviceslip.com/advice";
        HttpResponseMessage response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            await ReplyAsync("Failed to fetch advice. Try again later.");
            return;
        }
        string json = await response.Content.ReadAsStringAsync();
        JObject data = JObject.Parse(json);
        string advice = data["slip"]?["advice"]?.ToString() ?? "No advice found.";
        await ReplyAsync(advice);
    }

    [Name("Random Joke")]
    [Summary("Fetches a random joke.")]
    [Command("joke")]
    [Alias("getjoke", "randomjoke")]
    [RateLimit(3, 10)]
    public async Task RandomJoke()
    {
        string url = "https://official-joke-api.appspot.com/random_joke";
        HttpResponseMessage response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            await ReplyAsync("Failed to fetch a joke. Try again later.");
            return;
        }
        string json = await response.Content.ReadAsStringAsync();
        JObject data = JObject.Parse(json);
        string setup = data["setup"]?.ToString() ?? "No setup found.";
        string punchline = data["punchline"]?.ToString() ?? "No punchline found.";
        await ReplyAsync($"{setup}\n\n||{punchline}||");
    }

    [Name("Random Fact")]
    [Summary("Fetches a random fact.")]
    [Command("fact")]
    [Alias("getfact", "randomfact")]
    [RateLimit(3, 10)]
    public async Task RandomFact()
    {
        string url = "https://uselessfacts.jsph.pl/random.json?language=en";
        HttpResponseMessage response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            await ReplyAsync("Failed to fetch a fact. Try again later.");
            return;
        }
        string json = await response.Content.ReadAsStringAsync();
        JObject data = JObject.Parse(json);
        string fact = data["text"]?.ToString() ?? "No fact found.";
        await ReplyAsync(fact);
    }
}
