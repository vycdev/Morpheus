using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Morpheus.Database;
using Morpheus.Handlers;
using Morpheus.Jobs;
using Morpheus.Services;
using Morpheus.Utilities;
using Quartz;

// Load environment variables from .env file
Env.Load(".env");

GatewayIntents intents =
    GatewayIntents.Guilds
    | GatewayIntents.GuildMembers
    | GatewayIntents.GuildMessages
    | GatewayIntents.MessageContent
    | GatewayIntents.GuildMessageReactions
    | GatewayIntents.DirectMessages
    | GatewayIntents.DirectMessageReactions;

// Set up configs
DiscordSocketConfig clientConfig = new()
{
    MessageCacheSize = 100,
    AlwaysDownloadUsers = true,
    LogLevel = LogSeverity.Verbose,
    GatewayIntents = intents,

#if DEBUG
    UseInteractionSnowflakeDate = false,
#endif
};

CommandServiceConfig commandServiceConfig = new()
{
    LogLevel = LogSeverity.Verbose,
    DefaultRunMode = Discord.Commands.RunMode.Async,
};

// Set up dependency injection
IServiceCollection services = new ServiceCollection();

// Main services
services.AddSingleton(clientConfig);
services.AddSingleton<DiscordSocketClient>();

services.AddSingleton(commandServiceConfig);
services.AddSingleton<CommandService>();

// Scoped Services
services.AddScoped<GuildService>();
services.AddScoped<UsersService>();
services.AddScoped<LogsService>();
services.AddScoped<ActivityService>();
services.AddScoped<ChannelService>();
services.AddScoped<EconomyService>();
services.AddScoped<StocksService>();
services.AddScoped<BotAvatarJob>();
services.AddScoped<TemporaryBansJob>();
    services.AddScoped<HoneypotRenameJob>();
    services.AddScoped<StockUpdateJob>();
    services.AddScoped<UbiJob>();

    // Add Quartz
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
        .WithCronSchedule("0 0 0 * * ?") // every day at 00:00 UTC
    );

    q.ScheduleJob<TemporaryBansJob>(trigger => trigger
        .WithIdentity("temporaryBansDaily", "discord")
        .StartNow()
        .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromDays(1)).RepeatForever())
    );

    q.ScheduleJob<HoneypotRenameJob>(trigger => trigger
        .WithIdentity("honeypotRenameDaily", "discord")
        .StartNow()
        .WithCronSchedule("0 5 0 * * ?") // daily at 00:05 UTC
    );

    q.ScheduleJob<UbiJob>(trigger => trigger
        .WithIdentity("ubiDistribution", "discord")
        .StartNow()
        .WithCronSchedule("0 0 0 * * ?") // every day at 00:00 UTC
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

// Add the handlers (singletons that create a scope per event)
services.AddSingleton<MessagesHandler>();
services.AddSingleton<WelcomeHandler>();
services.AddSingleton<HoneypotHandler>();
services.AddSingleton<InteractionsHandler>();
services.AddSingleton<LogsHandler>();
services.AddSingleton<ActivityHandler>();
services.AddSingleton<FunnyResponsesHandler>();
services.AddSingleton<ReactionRolesHandler>();


// Add the database context
services.AddDbContextPool<DB>(options => options.UseNpgsql(Env.Variables["DB_CONNECTION_STRING"])
            .ConfigureWarnings(c => c.Ignore(RelationalEventId.CommandExecuted)));

IHost host = Host.CreateDefaultBuilder().ConfigureServices((ctx, srv) =>
{
    foreach (ServiceDescriptor service in services)
        srv.Add(service);
}).Build();

// Run database migrations within a scope
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DB>();
    db.Database.Migrate();

    const string balanceBackfillKey = "balance_backfill_v1";
    var backfill = db.BotSettings.FirstOrDefault(s => s.Key == balanceBackfillKey);
    if (backfill == null)
    {
        int updated = db.Database.ExecuteSqlRaw("UPDATE \"Users\" SET \"Balance\" = 1000.00 WHERE \"Balance\" = 0");
        db.BotSettings.Add(new Morpheus.Database.Models.BotSetting
        {
            Key = balanceBackfillKey,
            Value = updated.ToString(),
            UpdateDate = DateTime.UtcNow
        });
        db.SaveChanges();
    }
}

// Start the handlers 
_ = host.Services.GetRequiredService<LogsHandler>();
_ = host.Services.GetRequiredService<WelcomeHandler>();
_ = host.Services.GetRequiredService<HoneypotHandler>();
_ = host.Services.GetRequiredService<InteractionsHandler>();
_ = host.Services.GetRequiredService<ActivityHandler>();
_ = host.Services.GetRequiredService<FunnyResponsesHandler>();
_ = host.Services.GetRequiredService<ReactionRolesHandler>();
// ReactionsHandler intentionally not started: approval flows use interaction buttons now.
MessagesHandler messagesHandler = host.Services.GetRequiredService<MessagesHandler>();

// Register the commands 
await messagesHandler.InstallCommands();

// Start the bot
DiscordSocketClient client = host.Services.GetRequiredService<DiscordSocketClient>();

await client.LoginAsync(TokenType.Bot, Env.Variables["BOT_TOKEN"]);
await client.StartAsync();

// Keep the app running
await host.RunAsync();
