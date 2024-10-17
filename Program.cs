using Discord.WebSocket;
using Discord;
using Morpheus.Utilities;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Morpheus.Services;
using Morpheus.Database;
using Microsoft.EntityFrameworkCore;
using Morpheus.Handlers;
using Discord.Interactions;

// Load environment variables from .env file
Env.Load(".env");

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
    DefaultRunMode = Discord.Commands.RunMode.Async,
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
services.AddSingleton<InteractionHandler>();

// Add the logger service
services.AddSingleton<LoggerService>();

// Add the database context
services.AddDbContextPool<DB>(options => options.UseNpgsql(Env.Variables["DB_CONNECTION_STRING"]));

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

await client.LoginAsync(TokenType.Bot, Env.Variables["BOT_TOKEN"]);
await client.StartAsync();

// Keep the app running
await host.RunAsync();
