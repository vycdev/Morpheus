using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Morpheus.Extensions;
using Morpheus.MCP;
using Morpheus.Utilities;

Env.Load(".env");

McpApiOptions mcpOptions = McpApiOptions.FromEnvironment();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(mcpOptions.ListenerUrls);

builder.Services
    .AddBotServices()
    .AddBotJobs()
    .AddBotHandlers()
    .AddBotDatabase()
    .AddMcpApi(mcpOptions);

WebApplication app = builder.Build();

if (mcpOptions.Enabled)
{
    app.UseCors();
    app.UseMcpApiSecurity();
    app.MapMcpApi();
}

app.RunStartupMigrations();
await app.StartBotAsync();
await app.RunAsync();
