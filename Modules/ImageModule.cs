using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Morpheus.Attributes;
using Morpheus.Database;
using Morpheus.Extensions;
using Morpheus.Handlers;
using Morpheus.Utilities.Images;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Morpheus.Modules;

public class ImageModule(DiscordSocketClient client, CommandService commands, InteractionsHandler interactionHandler, IServiceProvider serviceProvider, DB dbContext) : ModuleBase<SocketCommandContextExtended>
{
    private static readonly HttpClient httpClient = new();

    private readonly CommandService commands = commands;
    private readonly IServiceProvider serviceProvider = serviceProvider;
    private readonly DB dbContext = dbContext;

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
            string responseString = await httpClient.GetStringAsync(apiUrl);

            using JsonDocument jsonDoc = JsonDocument.Parse(responseString);
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

                    Embed embed = new EmbedBuilder()
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

    [Name("Random Dog")]
    [Summary("Sends a random dog image.")]
    [Command("dog")]
    [Alias("dogs")]
    [RequireBotPermission(GuildPermission.EmbedLinks)]
    [RateLimit(5, 10)]
    public async Task DogAsync()
    {
        try
        {
            string apiUrl = "https://dog.ceo/api/breeds/image/random";
            string responseString = await httpClient.GetStringAsync(apiUrl);

            using JsonDocument jsonDoc = JsonDocument.Parse(responseString);
            JsonElement root = jsonDoc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("message", out JsonElement urlElement) && urlElement.ValueKind == JsonValueKind.String)
                {
                    string imageUrl = urlElement.GetString()!;

                    if (string.IsNullOrWhiteSpace(imageUrl))
                    {
                        await ReplyAsync("The dog API returned an empty image URL. Try again!");
                        return;
                    }

                    Embed embed = new EmbedBuilder()
                        .WithTitle("Here's a random dog!")
                        .WithImageUrl(imageUrl)
                        .WithColor(Color.Blue) // Or any color you like
                        .WithFooter("Powered by dog.ceo")
                        .Build();

                    await ReplyAsync(embed: embed);
                }
                else
                {
                    await ReplyAsync("Could not find the image URL in the API response. The API structure might have changed.");
                    Console.WriteLine($"[DOG API ERROR] Unexpected JSON structure. URL property missing. Response: {responseString}");
                }
            }
            else
            {
                await ReplyAsync("The dog API did not return any images. Try again!");
                Console.WriteLine($"[DOG API ERROR] Empty array or not an array. Response: {responseString}");
            }
        }
        catch (HttpRequestException httpEx)
        {
            Console.WriteLine($"[DOG API HTTP ERROR] {httpEx}");
            await ReplyAsync("Sorry, I couldn't connect to the cat API right now. Please try again later.");
        }
        catch (JsonException jsonEx)
        {
            Console.WriteLine($"[DOG API JSON ERROR] {jsonEx}");
            await ReplyAsync("Sorry, I received an unexpected response from the cat API. Please try again later.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DOG COMMAND UNEXPECTED ERROR] {ex}");
            await ReplyAsync($"An unexpected error occurred while fetching a cat image. The hoomans have been notified (not really, but they should check the logs).");
        }
    }

    [Name("Deepfry")]
    [Summary("Deepfry an image.")]
    [Command("deepfry")]
    [Alias("deepfryimage", "deepfryimg")]
    [RequireBotPermission(GuildPermission.EmbedLinks)]
    [RequireUserPermission(GuildPermission.AttachFiles)]
    [RateLimit(3, 30)]
    public async Task DeepfryAsync()
    {
        // Check if the user has attached an image
        if (Context.Message.Attachments.Count == 0)
        {
            await ReplyAsync("Please attach an image to deepfry.");
            return;
        }
        // Get the first attachment
        Attachment attachment = Context.Message.Attachments.First();
        byte[] imageBytes;

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(attachment.Url);
            response.EnsureSuccessStatusCode();
            imageBytes = await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEEPFRY ERROR] Failed to download image: {ex}");
            await ReplyAsync("Failed to download the image. Please try again later.");
            return;
        }

        byte[] deepfriedImage = ImageDeepFryer.DeepFry(imageBytes);

        if (deepfriedImage == null || deepfriedImage.Length == 0)
        {
            await ReplyAsync("Failed to deepfry the image. Please try again with a different image.");
            return;
        }

        // Upload the deepfried image to Discord
        MemoryStream stream = new(deepfriedImage);
        Discord.Rest.RestUserMessage uploadResult = await Context.Channel.SendFileAsync(stream, "deepfried_image.png", "Here's your deepfried image!", isSpoiler: false);
        stream.Dispose();

        if (uploadResult == null)
        {
            await ReplyAsync("Failed to upload the deepfried image. Please try again later.");
            return;
        }
    }

    [Name("Get Captcha")]
    [Summary("Get a captcha image to prove you're not a bot.")]
    [Command("captcha")]
    [Alias("getcaptcha")]
    [RequireBotPermission(GuildPermission.EmbedLinks)]
    [RateLimit(3, 30)]
    public async Task GetCaptchaAsync()
    {
        // Generate a captcha image
        CaptchaResult captchaImage = (new CaptchaGenerator()).GenerateCaptcha();
        if (captchaImage == null)
        {
            await ReplyAsync("Failed to generate a captcha image. Please try again later.");
            return;
        }

        // Upload the captcha image to Discord
        MemoryStream stream = new(captchaImage.CaptchaImageBytes);
        Discord.Rest.RestUserMessage uploadResult = await Context.Channel.SendFileAsync(stream, "captcha.png",
            $"Here's your captcha image containing the text: {captchaImage.CaptchaText}", isSpoiler: false);

        stream.Dispose();
        if (uploadResult == null)
        {
            await ReplyAsync("Failed to upload the captcha image. Please try again later.");
            return;
        }
    }

    [Name("QR Code")]
    [Summary("Generates a QR code for the provided text.")]
    [Command("qrcode")]
    [Alias("generateqrcode", "makeqrcode")]
    [RequireBotPermission(GuildPermission.EmbedLinks)]
    [RateLimit(3, 30)]
    public async Task GenerateQRCodeAsync([Remainder] string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            await ReplyAsync("Please provide some text to generate a QR code.");
            return;
        }
        try
        {
            byte[] qrCodeImage = QrCodeService.GenerateQrCodeWithColors(text);
            if (qrCodeImage == null || qrCodeImage.Length == 0)
            {
                await ReplyAsync("Failed to generate the QR code. Please try again later.");
                return;
            }

            // Upload the QR code image to Discord
            MemoryStream stream = new(qrCodeImage);
            Discord.Rest.RestUserMessage uploadResult = await Context.Channel.SendFileAsync(stream, "qrcode.png", "Here's your QR code!", isSpoiler: false);
            stream.Dispose();
            if (uploadResult == null)
            {
                await ReplyAsync("Failed to upload the QR code image. Please try again later.");
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QR CODE ERROR] {ex}");
            await ReplyAsync("An error occurred while generating the QR code. Please try again later.");
        }
    }

    [Name("Blurpify")]
    [Summary("Pixelate + small random warps on an attached image.")]
    [Command("blurpify")]
    [Alias("blur", "blrp")]
    [RequireBotPermission(GuildPermission.EmbedLinks)]
    [RequireUserPermission(GuildPermission.AttachFiles)]
    [RateLimit(3, 30)]
    public async Task BlurpifyAsync(int pixelScale = 4, int maxOffset = 6, int smoothPasses = 2)
    {
        // Need an attached image
        if (Context.Message.Attachments.Count == 0)
        {
            await ReplyAsync("Please attach an image to blurpify.");
            return;
        }

        Attachment attachment = Context.Message.Attachments.First();
        byte[] imageBytes;

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(attachment.Url);
            response.EnsureSuccessStatusCode();
            imageBytes = await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BLURPIFY ERROR] Failed to download image: {ex}");
            await ReplyAsync("Failed to download the image. Please try again later.");
            return;
        }

        byte[] outBytes;
        try
        {
            outBytes = Blurpifier.Blurpify(imageBytes, pixelScale, maxOffset, smoothPasses);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BLURPIFY ERROR] Processing failed: {ex}");
            await ReplyAsync("Failed to process the image. Try different parameters or a different image.");
            return;
        }

        if (outBytes == null || outBytes.Length == 0)
        {
            await ReplyAsync("Processing produced no output. Try again with different parameters.");
            return;
        }

        using MemoryStream stream = new(outBytes);
        Discord.Rest.RestUserMessage uploadResult = await Context.Channel.SendFileAsync(stream, "blurpified.png", "Here's your blurpified image!", isSpoiler: false);
        if (uploadResult == null)
        {
            await ReplyAsync("Failed to upload the processed image. Please try again later.");
            return;
        }
    }
}
