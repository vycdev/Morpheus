using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Handlers;
using Morpheus.Jobs;
using Morpheus.Services;
using Morpheus.Utilities;
using Quartz;

namespace Morpheus.Extensions;

public static class BotStartupExtensions
{
    public static IServiceCollection AddBotServices(this IServiceCollection services)
    {
        services.AddSingleton(CreateDiscordSocketConfig());
        services.AddSingleton<DiscordSocketClient>();

        services.AddSingleton(CreateCommandServiceConfig());
        services.AddSingleton<CommandService>();
        services.AddSingleton<GuildPrefixService>();

        services.AddScoped<GuildService>();
        services.AddScoped<UsersService>();
        services.AddSingleton<LogQueue>();
        services.AddSingleton<LogsService>();
        services.AddHostedService<LogsWriterService>();
        services.AddScoped<ActivityService>();
        services.AddScoped<ActivityGraphService>();
        services.AddScoped<ActivityLeaderboardService>();
        services.AddScoped<ActivityScoringService>();
        services.AddScoped<ActivityLevelService>();
        services.AddScoped<ChannelService>();
        services.AddScoped<EconomyService>();
        services.AddScoped<StocksService>();
        services.AddScoped<QuoteService>();
        services.AddSingleton<SlotsService>();

        return services;
    }

    public static IServiceCollection AddBotJobs(this IServiceCollection services)
    {
        services.AddScoped<BotAvatarJob>();
        services.AddScoped<TemporaryBansJob>();
        services.AddScoped<HoneypotRenameJob>();
        services.AddScoped<StockUpdateJob>();
        services.AddScoped<UbiJob>();
        services.AddScoped<WealthTaxJob>();

        services.AddQuartz(q =>
        {
            q.ScheduleJob<BotActivityJob>(trigger => trigger
                .WithIdentity("every6hours", "discord")
                .StartNow()
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromHours(6)).RepeatForever())
            );

            q.ScheduleJob<DeleteOldLogsJob>(trigger => trigger
                .WithIdentity("deleteOldLogs", "discord")
                .StartNow()
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromDays(1)).RepeatForever())
            );

            q.ScheduleJob<ActivityRolesJob>(trigger => trigger
                .WithIdentity("activityRoles", "discord")
                .StartAt(DateTime.UtcNow.AddMinutes(1))
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromDays(1)).RepeatForever())
            );

            q.ScheduleJob<RemindersJob>(trigger => trigger
                .WithIdentity("reminders", "discord")
                .StartNow()
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromMinutes(1)).RepeatForever())
            );

            q.ScheduleJob<BotAvatarJob>(trigger => trigger
                .WithIdentity("botAvatarDaily", "discord")
                .StartNow()
                .WithCronSchedule("0 0 0 * * ?")
            );

            q.ScheduleJob<TemporaryBansJob>(trigger => trigger
                .WithIdentity("temporaryBansDaily", "discord")
                .StartNow()
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromDays(1)).RepeatForever())
            );

            q.ScheduleJob<HoneypotRenameJob>(trigger => trigger
                .WithIdentity("honeypotRenameDaily", "discord")
                .StartNow()
                .WithCronSchedule("0 5 0 * * ?")
            );

            q.ScheduleJob<UbiJob>(trigger => trigger
                .WithIdentity("ubiDistribution", "discord")
                .StartNow()
                .WithCronSchedule("0 0 0 * * ?")
            );

            q.ScheduleJob<WealthTaxJob>(trigger => trigger
                .WithIdentity("wealthTax", "discord")
                .StartNow()
                .WithCronSchedule("0 30 23 * * ?")
            );

            q.ScheduleJob<StockUpdateJob>(trigger => trigger
                .WithIdentity("stockUpdate", "discord")
                .StartAt(DateTime.UtcNow.AddMinutes(2))
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromMinutes(5)).RepeatForever())
            );
        });

        services.AddQuartzHostedService(options =>
        {
            options.WaitForJobsToComplete = true;
        });

        return services;
    }

    public static IServiceCollection AddBotHandlers(this IServiceCollection services)
    {
        services.AddSingleton<MessagesHandler>();
        services.AddSingleton<WelcomeHandler>();
        services.AddSingleton<HoneypotHandler>();
        services.AddSingleton<InteractionsHandler>();
        services.AddSingleton<LogsHandler>();
        services.AddSingleton<ActivityHandler>();
        services.AddSingleton<FunnyResponsesHandler>();
        services.AddSingleton<ReactionRolesHandler>();

        return services;
    }

    public static IServiceCollection AddBotDatabase(this IServiceCollection services)
    {
        services.AddDbContextPool<DB>(options => options
            .UseNpgsql(Env.Variables["DB_CONNECTION_STRING"])
            .ConfigureWarnings(c => c.Ignore(RelationalEventId.CommandExecuted)));

        return services;
    }

    public static void RunStartupMigrations(this IHost host)
    {
        using IServiceScope scope = host.Services.CreateScope();
        DB db = scope.ServiceProvider.GetRequiredService<DB>();

        db.Database.Migrate();
        RunBalanceBackfill(db);
    }

    public static async Task StartBotAsync(this IHost host)
    {
        IServiceProvider services = host.Services;

        _ = services.GetRequiredService<LogsHandler>();
        _ = services.GetRequiredService<WelcomeHandler>();
        _ = services.GetRequiredService<HoneypotHandler>();
        _ = services.GetRequiredService<InteractionsHandler>();
        _ = services.GetRequiredService<ActivityHandler>();
        _ = services.GetRequiredService<FunnyResponsesHandler>();
        _ = services.GetRequiredService<ReactionRolesHandler>();
        // ReactionsHandler intentionally not started: approval flows use interaction buttons now.

        MessagesHandler messagesHandler = services.GetRequiredService<MessagesHandler>();
        await messagesHandler.InstallCommands();

        DiscordSocketClient client = services.GetRequiredService<DiscordSocketClient>();
        await client.LoginAsync(TokenType.Bot, Env.Variables["BOT_TOKEN"]);
        await client.StartAsync();
    }

    private static DiscordSocketConfig CreateDiscordSocketConfig()
    {
        GatewayIntents intents =
            GatewayIntents.Guilds
            | GatewayIntents.GuildMembers
            | GatewayIntents.GuildMessages
            | GatewayIntents.MessageContent
            | GatewayIntents.GuildMessageReactions
            | GatewayIntents.DirectMessages
            | GatewayIntents.DirectMessageReactions;

        return new DiscordSocketConfig
        {
            MessageCacheSize = 100,
            AlwaysDownloadUsers = true,
            LogLevel = LogSeverity.Verbose,
            GatewayIntents = intents,

#if DEBUG
            UseInteractionSnowflakeDate = false,
#endif
        };
    }

    private static CommandServiceConfig CreateCommandServiceConfig()
    {
        return new CommandServiceConfig
        {
            LogLevel = LogSeverity.Verbose,
            DefaultRunMode = Discord.Commands.RunMode.Async,
        };
    }

    private static void RunBalanceBackfill(DB db)
    {
        const string balanceBackfillKey = "balance_backfill_v1";
        BotSetting? backfill = db.BotSettings.FirstOrDefault(s => s.Key == balanceBackfillKey);

        if (backfill != null)
            return;

        int updated = db.Database.ExecuteSqlRaw("UPDATE \"Users\" SET \"Balance\" = 1000.00 WHERE \"Balance\" = 0");
        db.BotSettings.Add(new BotSetting
        {
            Key = balanceBackfillKey,
            Value = updated.ToString(),
            UpdateDate = DateTime.UtcNow
        });
        db.SaveChanges();
    }
}
