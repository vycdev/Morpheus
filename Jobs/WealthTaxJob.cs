using Morpheus.Services;
using Quartz;

namespace Morpheus.Jobs;

[DisallowConcurrentExecution]
public class WealthTaxJob(EconomyService economyService) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await economyService.CollectWealthTax();
    }
}
