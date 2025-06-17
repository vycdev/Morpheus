using Discord.WebSocket;
using Discord;
using Quartz;
using Morpheus.Services;
using Morpheus.Database;
using Microsoft.EntityFrameworkCore;

namespace Morpheus.Jobs;

public class DeleteOldLogsJob(LogsService logsService, DB dB) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        DateTime date  = DateTime.UtcNow.AddDays(-30); // Adjust the time span as needed

        int count = await dB.Logs
            .Where(log => log.InsertDate < date) // Adjust the time span as needed
            .ExecuteDeleteAsync();

        logsService.Log($"Quartz Job - Deleted {count} old logs.");
    }
}
