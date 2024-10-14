using Discord.WebSocket;
using Discord;
using Morpheus.Utilities;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using Morpheus.Services;
using System;
using Morpheus.Database;
using Microsoft.EntityFrameworkCore;

// Load environment variables from .env file
EnvReader.Load(".env");


// Set up configs
DiscordSocketConfig clientConfig = new()
{
    MessageCacheSize = 100,
    AlwaysDownloadUsers = true,
    LogLevel = LogSeverity.Verbose,
    GatewayIntents = GatewayIntents.All, 
};

CommandServiceConfig commandServiceConfig = new()
{
    LogLevel = LogSeverity.Verbose,
    DefaultRunMode = RunMode.Async,
};

// Set up dependency injection
IServiceCollection services = new ServiceCollection();

// Main services
services.AddSingleton(clientConfig);
services.AddSingleton<DiscordSocketClient>();

services.AddSingleton(commandServiceConfig);
services.AddSingleton<CommandService>();

// Add the logger service
services.AddSingleton<LoggerService>();

// Add the database context
services.AddDbContextPool<Morpheus.Database.DbContext>(options => options.UseNpgsql(Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")));

IServiceProvider serviceProvider = services.BuildServiceProvider();

// Start the logger service
_ = serviceProvider.GetRequiredService<LoggerService>();

// Start the bot
DiscordSocketClient client = serviceProvider.GetRequiredService<DiscordSocketClient>();

await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("BOT_TOKEN"));
await client.StartAsync();

// Block this task until the program is closed.
await Task.Delay(-1);
