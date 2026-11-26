using Discord;
using Discord.Commands;
using Morpheus.Attributes;
using Morpheus.Extensions;
using Morpheus.Services;
using Morpheus.Utilities.Extensions;
using System.Globalization;

namespace Morpheus.Modules;

[Name("Economy")]
[Summary("Commands related to the server economy.")]
public class EconomyModule(EconomyService economyService, UsersService usersService) : MorpheusModuleBase
{
    [Name("UBI Status")]
    [Summary("Check the current Universal Basic Income pool and time until next distribution.")]
    [Command("ubi")]
    [Alias("ubistatus", "basicincome")]
    [RateLimit(3, 10)]
    public async Task UbiStatus()
    {
        decimal poolAmount = await economyService.GetPoolAmount();

        // Calculate time until next midnight UTC
        DateTime now = DateTime.UtcNow;
        DateTime nextMidnight = now.Date.AddDays(1);
        string timeUntil = now.GetAccurateTimeSpan(nextMidnight);

        // Estimate per user (rough estimate)
        // We can't get exact user count easily here without DB context injection just for this, 
        // but users can do the math or we can inject DB.
        // Let's keep it simple and just show the total pool.

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle("💰 Universal Basic Income Pool")
            .WithDescription($"The current community pot is **${poolAmount:F2}**.\n\nThis amount will be distributed equally to all users in **{timeUntil}**.")
            .WithColor(Color.Gold)
            .WithFooter("All fees and taxes fund this pool.")
            .WithCurrentTimestamp();

        await ReplyAsync(embed: embed.Build());
    }

    [Name("Donate to UBI")]
    [Summary("Donate money in whole cents to the Universal Basic Income pool.")]
    [Command("ubi donate")]
    [Alias("donate")]
    public async Task Donate(
        [OverrideTypeReader(typeof(DonationAmountReader))] [RequireWholeCentDonation] DonationAmount amount)
    {
        if (amount.Value <= 0)
        {
            await ReplyAsync("Please specify a positive amount to donate.");
            return;
        }

        var dbUser = await usersService.TryGetCreateUser(Context.User);
        var (success, message) = await economyService.DonateToUbi(dbUser.Id, amount.Value);

        if (success)
        {
            await ReplyAsync($"✅ {message}");
        }
        else
        {
            await ReplyAsync($"❌ {message}");
        }
    }

    [Name("UBI Leaderboard")]
    [Summary("Shows the top donors to the UBI pool.")]
    [Command("ubi leaderboard")]
    [Alias("ubi top", "ubidonors", "donors")]
    public async Task UbiLeaderboard()
    {
        var topDonors = await economyService.GetTopDonors(10);

        if (topDonors.Count == 0)
        {
            await ReplyAsync("No donations have been made yet. Be the first!");
            return;
        }

        EmbedBuilder embed = new EmbedBuilder()
            .WithTitle("🏆 UBI Donation Leaderboard")
            .WithDescription("Top philanthropists who have contributed to the community pool.")
            .WithColor(Color.Gold)
            .WithCurrentTimestamp();

        int rank = 1;
        foreach (var donor in topDonors)
        {
            string medal = rank switch
            {
                1 => "🥇",
                2 => "🥈",
                3 => "🥉",
                _ => $"#{rank}"
            };

            embed.AddField($"{medal} {donor.Username}", $"**${donor.TotalDonated:F2}**", true);
            rank++;
        }

        await ReplyAsync(embed: embed.Build());
    }

    [Name("Rob User")]
    [Summary("Attempt to rob another user. 40% chance of success (20% if they were recently robbed).")]
    [Command("rob")]
    [Alias("steal", "pickpocket")]
    public async Task Rob(IUser target)
    {
        if (target.Id == Context.User.Id)
        {
            await ReplyAsync("You cannot rob yourself.");
            return;
        }

        if (target.IsBot)
        {
            await ReplyAsync("You cannot rob bots.");
            return;
        }

        var dbRobber = await usersService.TryGetCreateUser(Context.User);
        var dbVictim = await usersService.TryGetCreateUser(target);

        var (success, message) = await economyService.RobUser(dbRobber.Id, dbVictim.Id);

        if (success)
        {
            await ReplyAsync(message);
        }
        else
        {
            await ReplyAsync($"🚫 {message}");
        }
    }

    public readonly record struct DonationAmount(decimal Value, bool HasFractionalCent);

    private sealed class RequireWholeCentDonationAttribute : ParameterPreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(
            ICommandContext context, ParameterInfo parameter, object value, IServiceProvider services) =>
            Task.FromResult(value is DonationAmount { HasFractionalCent: true }
                ? PreconditionResult.FromError(EconomyService.WholeCentDonationMessage)
                : PreconditionResult.FromSuccess());
    }

    private sealed class DonationAmountReader : TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            NumberFormatInfo format = CultureInfo.CurrentCulture.NumberFormat;
            if (!decimal.TryParse(input, NumberStyles.Number, format, out decimal amount))
                return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Failed to parse Decimal."));

            bool hasFractionalCent = !decimal.IsNegative(amount) && HasFractionalCent(input, format);
            return Task.FromResult(TypeReaderResult.FromSuccess(new DonationAmount(amount, hasFractionalCent)));
        }

        private static bool HasFractionalCent(ReadOnlySpan<char> input, NumberFormatInfo format)
        {
            // Syntax is valid. Find the original fraction because decimal.TryParse can round it away.
            const string whitespace = " \t\r\n\v\f";
            input = input.TrimStart(whitespace);
            if (TryConsume(ref input, format.PositiveSign) && format.NumberNegativePattern == 2)
                input = input.TrimStart(whitespace);

            while (!input.IsEmpty)
            {
                if (char.IsAsciiDigit(input[0]))
                {
                    input = input[1..];
                    continue;
                }

                if (TryConsume(ref input, format.NumberDecimalSeparator))
                {
                    for (int i = 2; i < input.Length && char.IsAsciiDigit(input[i]); i++)
                    {
                        if (input[i] != '0')
                            return true;
                    }
                    return false;
                }

                if (!TryConsume(ref input, format.NumberGroupSeparator))
                    break;
            }
            return false;
        }

        private static bool TryConsume(ref ReadOnlySpan<char> input, string token)
        {
            if (token.Length == 0 || token.Length > input.Length)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                // decimal.TryParse accepts a regular space in place of either nonbreaking space.
                if (input[i] != token[i] && !(input[i] == ' ' && token[i] is '\u00A0' or '\u202F'))
                    return false;
            }

            input = input[token.Length..];
            return true;
        }
    }
}
