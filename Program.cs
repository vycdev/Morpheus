using Microsoft.Extensions.Hosting;
using Morpheus.Extensions;
using Morpheus.Utilities;

Env.Load(".env");

IHost host = Host.CreateDefaultBuilder()
    .ConfigureServices((_, services) =>
    {
        services
            .AddBotServices()
            .AddBotJobs()
            .AddBotHandlers()
            .AddBotDatabase();
    })
    .Build();

host.RunStartupMigrations();
await host.StartBotAsync();
await host.RunAsync();
