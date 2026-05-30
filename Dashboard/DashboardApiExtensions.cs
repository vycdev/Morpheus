using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Morpheus.Services;
using Morpheus.Utilities;

namespace Morpheus.Dashboard;

public static class DashboardApiExtensions
{
    private const string CorsPolicyName = "DashboardCors";

    public static IServiceCollection AddDashboardApi(
        this IServiceCollection services,
        DashboardApiOptions options)
    {
        services.AddSingleton(options);
        services.AddScoped<DashboardStatsService>();

        services.ConfigureHttpJsonOptions(jsonOptions =>
        {
            jsonOptions.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });

        services.AddCors(corsOptions =>
        {
            corsOptions.AddPolicy(CorsPolicyName, policy =>
            {
                if (options.CorsOrigins.Length > 0)
                {
                    policy
                        .WithOrigins(options.CorsOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }
            });
        });

        return services;
    }

    public static WebApplication MapDashboardApi(this WebApplication app)
    {
        app.MapGet("/", (DashboardApiOptions options) => Results.Ok(new
        {
            service = "Morpheus Dashboard API",
            dashboard = "http://127.0.0.1:3000",
            health = "/api/dashboard/health",
            api = "/api/dashboard",
            authEnabled = !string.IsNullOrWhiteSpace(options.ApiKey)
        }));

        RouteGroupBuilder api = app
            .MapGroup("/api/dashboard")
            .RequireCors(CorsPolicyName)
            .AddEndpointFilter(RequireDashboardApiKey);

        api.MapGet("/health", (DashboardApiOptions options) => Results.Ok(new
        {
            status = "ok",
            startedAtUtc = Env.StartTime,
            urls = options.Urls,
            authEnabled = !string.IsNullOrWhiteSpace(options.ApiKey)
        }));

        api.MapGet("/overview", async (
            DashboardStatsService statsService,
            CancellationToken cancellationToken) =>
        {
            DashboardOverviewResponse overview = await statsService.GetOverviewAsync(cancellationToken);
            return Results.Ok(overview);
        });

        api.MapGet("/guilds", async (
            DashboardStatsService statsService,
            CancellationToken cancellationToken) =>
        {
            IReadOnlyList<DashboardGuildSummary> guilds = await statsService.GetGuildsAsync(cancellationToken);
            return Results.Ok(guilds);
        });

        api.MapGet("/activity", async (
            int? guildId,
            int? days,
            DashboardStatsService statsService,
            CancellationToken cancellationToken) =>
        {
            DashboardActivitySeriesResponse series = await statsService.GetActivitySeriesAsync(
                guildId,
                days ?? 30,
                cancellationToken);

            return Results.Ok(series);
        });

        api.MapGet("/leaderboard", async (
            int? guildId,
            string? metric,
            int? days,
            int? limit,
            DashboardStatsService statsService,
            CancellationToken cancellationToken) =>
        {
            DashboardLeaderboardResponse leaderboard = await statsService.GetActivityLeaderboardAsync(
                guildId,
                metric ?? "xp",
                days,
                limit ?? 10,
                cancellationToken);

            return Results.Ok(leaderboard);
        });

        api.MapGet("/quotes", async (
            int? guildId,
            int? page,
            string? sort,
            bool? approvedOnly,
            QuoteService quoteService) =>
        {
            QuotePage quotePage = await quoteService.GetQuotePageAsync(
                page ?? 1,
                sort ?? "newest",
                approvedOnly ?? true,
                guildId);

            DashboardQuotePageResponse response = new(
                quotePage.Page,
                quotePage.TotalPages,
                quotePage.Total,
                [.. quotePage.Items.Select(item => new DashboardQuoteItem(
                    item.Id,
                    item.GuildId,
                    item.UserId,
                    item.Author,
                    item.Content,
                    item.InsertDate,
                    item.Approved,
                    item.Removed,
                    item.Score))]);

            return Results.Ok(response);
        });

        api.MapGet("/quotes/{quoteId:int}", async (
            int quoteId,
            QuoteService quoteService) =>
        {
            QuoteDetails? quote = await quoteService.GetQuoteDetailsAsync(quoteId);

            return quote == null
                ? Results.NotFound()
                : Results.Ok(new DashboardQuoteDetailsResponse(
                    quote.Id,
                    quote.GuildId,
                    quote.Content,
                    quote.InsertDate,
                    quote.Approved,
                    quote.Removed,
                    quote.TotalScore,
                    quote.Author));
        });

        return app;
    }

    private static async ValueTask<object?> RequireDashboardApiKey(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        DashboardApiOptions options = context.HttpContext.RequestServices.GetRequiredService<DashboardApiOptions>();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            return await next(context);

        if (context.HttpContext.Request.Headers.TryGetValue("X-Dashboard-Key", out var apiKey) &&
            string.Equals(apiKey.ToString(), options.ApiKey, StringComparison.Ordinal))
        {
            return await next(context);
        }

        return Results.Unauthorized();
    }
}
