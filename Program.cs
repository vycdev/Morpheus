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

// Set up configs
DiscordSocketConfig clientConfig = new()
{
    MessageCacheSize = 100,
    AlwaysDownloadUsers = true,
    LogLevel = LogSeverity.Verbose,
    GatewayIntents = GatewayIntents.All,

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
        .StartAt((DateTime.UtcNow.AddSeconds(10)))
        .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromDays(1)).RepeatForever())
    );

    q.ScheduleJob<RemindersJob>(trigger => trigger
        .WithIdentity("reminders", "discord")
        .StartNow()
        .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromMinutes(1)).RepeatForever())
    );
});

services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});

// Add the handlers
services.AddScoped<MessagesHandler>();
services.AddScoped<WelcomeHandler>();
services.AddScoped<InteractionsHandler>();
services.AddScoped<LogsHandler>();
services.AddScoped<ActivityHandler>();

// Add the database context
services.AddDbContextPool<DB>(options => options.UseNpgsql(Env.Variables["DB_CONNECTION_STRING"])
            .ConfigureWarnings(c => c.Ignore(RelationalEventId.CommandExecuted)));

IHost host = Host.CreateDefaultBuilder().ConfigureServices((ctx, srv) =>
{
    foreach (ServiceDescriptor service in services)
        srv.Add(service);
}).Build();

// Run database migrations 
host.Services.GetRequiredService<DB>().Database.Migrate();

// Start the handlers 
_ = host.Services.GetRequiredService<LogsHandler>();
_ = host.Services.GetRequiredService<WelcomeHandler>();
_ = host.Services.GetRequiredService<InteractionsHandler>();
_ = host.Services.GetRequiredService<ActivityHandler>();
MessagesHandler messagesHandler = host.Services.GetRequiredService<MessagesHandler>();

// Register the commands 
await messagesHandler.InstallCommands();

// Start the bot
DiscordSocketClient client = host.Services.GetRequiredService<DiscordSocketClient>();

await client.LoginAsync(TokenType.Bot, Env.Variables["BOT_TOKEN"]);
await client.StartAsync();

// Keep the app running
await host.RunAsync();
