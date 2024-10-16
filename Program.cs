using Discord.WebSocket;
using Discord;
using Morpheus.Utilities;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Morpheus.Services;
using System;
using Morpheus.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Morpheus.Handlers;
using Discord.Interactions;


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

// Add the handlers
services.AddSingleton<CommandHandler>();

// Add the logger service
services.AddSingleton<LoggerService>();

// Add the database context
services.AddDbContextPool<DB>(options => options.UseNpgsql(Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")));

IHost host = Host.CreateDefaultBuilder().ConfigureServices((ctx, srv) => {
    foreach(ServiceDescriptor service in services) 
        srv.Add(service);
}).Build();

// Start the logger service
_ = host.Services.GetRequiredService<LoggerService>();

// Register the commands 
_ = host.Services.GetRequiredService<CommandHandler>().InstallCommandsAsync();

// Start the bot
DiscordSocketClient client = host.Services.GetRequiredService<DiscordSocketClient>();

await client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("BOT_TOKEN"));
await client.StartAsync();

// Keep the app running
await host.RunAsync();
