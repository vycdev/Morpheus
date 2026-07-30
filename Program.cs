using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Morpheus.Dashboard;
using Morpheus.Extensions;
using Morpheus.MCP;
using Morpheus.Utilities;

Env.Load(".env");

DashboardApiOptions dashboardOptions = DashboardApiOptions.FromEnvironment();
McpApiOptions mcpOptions = McpApiOptions.FromEnvironment();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(dashboardOptions.Urls);

builder.Services
    .AddBotServices()
    .AddBotJobs()
    .AddBotHandlers()
    .AddBotDatabase()
    .AddDashboardApi(dashboardOptions)
    .AddMcpApi(mcpOptions);

WebApplication app = builder.Build();

app.UseCors();
app.UseOutputCache();
app.MapDashboardApi();
app.MapMcpApi();

app.RunStartupMigrations();
await app.StartBotAsync();
await app.RunAsync();
