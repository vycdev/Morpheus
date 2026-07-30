using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Morpheus.Extensions;
using Morpheus.MCP;
using Morpheus.Utilities;

Env.Load(".env");

McpApiOptions mcpOptions = McpApiOptions.FromEnvironment();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(mcpOptions.Urls);

builder.Services
    .AddBotServices()
    .AddBotJobs()
    .AddBotHandlers()
    .AddBotDatabase()
    .AddMcpApi(mcpOptions);

WebApplication app = builder.Build();

app.UseCors();
app.MapMcpApi();

app.RunStartupMigrations();
await app.StartBotAsync();
await app.RunAsync();
