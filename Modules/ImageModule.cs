using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Extensions;
using Morpheus.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Morpheus.Modules;

public class ImageModule : ModuleBase<SocketCommandContextExtended>
{
    private static readonly HttpClient httpClient = new();

    private readonly CommandService commands;
    private readonly IServiceProvider serviceProvider;
    private readonly DB dbContext;

    public ImageModule(DiscordSocketClient client, CommandService commands, InteractionsHandler interactionHandler, IServiceProvider serviceProvider, DB dbContext)
    {
        this.commands = commands;
        this.serviceProvider = serviceProvider;
        this.dbContext = dbContext;
    }

    [Name("Random Cat")]
    [Summary("Sends a random cat image.")]
    [Command("cat")]
    [Alias("cats")]
    [RequireBotPermission(GuildPermission.EmbedLinks)]
    [RateLimit(5, 10)]
    public async Task CatAsync()
    {
        try
        {
            string apiUrl = "https://api.thecatapi.com/v1/images/search";
            var responseString = await httpClient.GetStringAsync(apiUrl);
            
            using (JsonDocument jsonDoc = JsonDocument.Parse(responseString))
            {
                JsonElement root = jsonDoc.RootElement;

                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    JsonElement firstCatObject = root[0]; // Get the first object in the array

                    if (firstCatObject.TryGetProperty("url", out JsonElement urlElement) && urlElement.ValueKind == JsonValueKind.String)
                    {
                        string imageUrl = urlElement.GetString()!;

                        if (string.IsNullOrWhiteSpace(imageUrl))
                        {
                            await ReplyAsync("The cat API returned an empty image URL. Try again!");
                            return;
                        }

                        var embed = new EmbedBuilder()
                            .WithTitle("Here's a random cat!")
                            .WithImageUrl(imageUrl)
                            .WithColor(Color.Blue) // Or any color you like
                            .WithFooter("Powered by thecatapi.com")
                            .Build();

                        await ReplyAsync(embed: embed);
                    }
                    else
                    {
                        await ReplyAsync("Could not find the image URL in the API response. The API structure might have changed.");
                        Console.WriteLine($"[CAT API ERROR] Unexpected JSON structure. URL property missing. Response: {responseString}");
                    }
                }
                else
                {
                    await ReplyAsync("The cat API did not return any images. Try again!");
                    Console.WriteLine($"[CAT API ERROR] Empty array or not an array. Response: {responseString}");
                }
            }
        }
        catch (HttpRequestException httpEx)
        {
            Console.WriteLine($"[CAT API HTTP ERROR] {httpEx}");
            await ReplyAsync("Sorry, I couldn't connect to the cat API right now. Please try again later.");
        }
        catch (JsonException jsonEx)
        {
            Console.WriteLine($"[CAT API JSON ERROR] {jsonEx}");
            await ReplyAsync("Sorry, I received an unexpected response from the cat API. Please try again later.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CAT COMMAND UNEXPECTED ERROR] {ex}");
            await ReplyAsync($"An unexpected error occurred while fetching a cat image. The hoomans have been notified (not really, but they should check the logs).");
        }
    }
}
