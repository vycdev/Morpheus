using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Morpheus.Dashboard;
using Morpheus.Extensions;
using Morpheus.Utilities;

Env.Load(".env");

DashboardApiOptions dashboardOptions = DashboardApiOptions.FromEnvironment();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(dashboardOptions.Urls);

builder.Services
    .AddBotServices()
    .AddBotJobs()
    .AddBotHandlers()
    .AddBotDatabase()
    .AddDashboardApi(dashboardOptions);

WebApplication app = builder.Build();

app.UseCors();
app.MapDashboardApi();

app.RunStartupMigrations();
await app.StartBotAsync();
await app.RunAsync();
