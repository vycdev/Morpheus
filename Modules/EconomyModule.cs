using Discord;
using Discord.Commands;
using Morpheus.Attributes;
using Morpheus.Extensions;
using Morpheus.Services;
using Morpheus.Utilities.Extensions;

namespace Morpheus.Modules;

[Name("Economy")]
[Summary("Commands related to the server economy.")]
public class EconomyModule(EconomyService economyService) : ModuleBase<SocketCommandContextExtended>
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
}
