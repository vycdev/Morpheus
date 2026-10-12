using Microsoft.Extensions.DependencyInjection;
using Morpheus.Extensions;
using Quartz;

namespace Morpheus.Tests;

public class BotStartupExtensionsTests
{
    [Fact]
    public async Task AddBotJobs_ConfiguresDailyCronFireTimesInUtc()
    {
        await using ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddBotJobs()
            .BuildServiceProvider();

        IScheduler scheduler = await services.GetRequiredService<ISchedulerFactory>().GetScheduler();

        try
        {
            (string Name, string Cron)[] expected =
            [
                ("botAvatarDaily", "0 0 0 * * ?"),
                ("honeypotRenameDaily", "0 5 0 * * ?"),
                ("ubiDistribution", "0 0 0 * * ?"),
                ("wealthTax", "0 30 23 * * ?")
            ];

            foreach ((string triggerName, string cronExpression) in expected)
            {
                ITrigger? trigger = await scheduler.GetTrigger(new TriggerKey(triggerName, "discord"));
                ICronTrigger cronTrigger = Assert.IsAssignableFrom<ICronTrigger>(trigger);

                Assert.Equal(TimeZoneInfo.Utc, cronTrigger.TimeZone);
                Assert.Equal(cronExpression, cronTrigger.CronExpressionString);
            }
        }
        finally
        {
            await scheduler.Shutdown();
        }
    }
}