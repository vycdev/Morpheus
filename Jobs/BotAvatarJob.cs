using System;
using System.IO;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Services;
using Quartz;

namespace Morpheus.Jobs;

public class BotAvatarJob : IJob
{
    private readonly DiscordSocketClient _client;
    private readonly DB _db;
    private readonly LogsService _logsService;

    public BotAvatarJob(DiscordSocketClient client, DB db, LogsService logsService)
    {
        _client = client;
        _db = db;
        _logsService = logsService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {

            var setting = await _db.BotSettings.FirstOrDefaultAsync(s => s.Key == "BotAvatar");
            string current = setting?.Value ?? "unknown";

            bool isDecember = DateTime.UtcNow.Month == 12;
            string targetKey = isDecember ? "xmas" : "default";
            string mediaDir = Path.Combine(AppContext.BaseDirectory, "Media");
            string fileName = targetKey == "xmas" ? "MorpheusXmas.png" : "Morpheus.png";
            string filePath = Path.Combine(mediaDir, fileName);

            if (current == targetKey)
                return;

            if (!File.Exists(filePath))
                return;

            await using var fs = File.OpenRead(filePath);
            await _client.CurrentUser.ModifyAsync(x => x.Avatar = new Image(fs));

            if (setting == null)
            {
                setting = new BotSetting { Key = "BotAvatar", Value = targetKey, UpdateDate = DateTime.UtcNow };
                _db.BotSettings.Add(setting);
            }
            else
            {
                setting.Value = targetKey;
                setting.UpdateDate = DateTime.UtcNow;
                _db.BotSettings.Update(setting);
            }

            await _db.SaveChangesAsync();
            _logsService.Log($"Quartz Job - Bot avatar updated to {targetKey}");
        }
        catch (Exception ex)
        {
            _logsService.Log($"Quartz Job - Error updating bot avatar: {ex}");
        }
    }
}
