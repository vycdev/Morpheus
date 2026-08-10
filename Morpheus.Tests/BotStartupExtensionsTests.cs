using Microsoft.Extensions.DependencyInjection;
using Morpheus.Extensions;
using Quartz;

namespace Morpheus.Tests;

public class BotStartupExtensionsTests
{
    [Fact]
    public async Task AddBotJobs_ConfiguresCronTriggersInUtc()
    {
        await using ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddBotJobs()
            .BuildServiceProvider();

        IScheduler scheduler = await services.GetRequiredService<ISchedulerFactory>().GetScheduler();

        try
        {
            string[] triggerNames = ["botAvatarDaily", "honeypotRenameDaily", "ubiDistribution", "wealthTax"];

            foreach (string triggerName in triggerNames)
            {
                ITrigger? trigger = await scheduler.GetTrigger(new TriggerKey(triggerName, "discord"));
                ICronTrigger cronTrigger = Assert.IsAssignableFrom<ICronTrigger>(trigger);

                Assert.Equal(TimeZoneInfo.Utc, cronTrigger.TimeZone);
            }
        }
        finally
        {
            await scheduler.Shutdown();
        }
    }
}