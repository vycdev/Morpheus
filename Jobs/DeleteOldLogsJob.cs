using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Morpheus.Database;
using Morpheus.Services;
using Quartz;

namespace Morpheus.Jobs;

public class DeleteOldLogsJob(LogsService logsService, DB dB) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        DateTime date = DateTime.UtcNow.AddDays(-30); // Adjust the time span as needed

        int count = await dB.Logs
            .Where(log => log.InsertDate < date) // Adjust the time span as needed
            .ExecuteDeleteAsync();

        logsService.Log($"Quartz Job - Deleted {count} old logs.");
    }
}
