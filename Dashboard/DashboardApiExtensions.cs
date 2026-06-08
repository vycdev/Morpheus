using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Morpheus.Services;
using Morpheus.Utilities;

namespace Morpheus.Dashboard;

public static class DashboardApiExtensions
{
    private const string CorsPolicyName = "DashboardCors";
    private const string ShortCachePolicyName = "DashboardShort";
    private const string SelectorCachePolicyName = "DashboardSelectors";

    public static IServiceCollection AddDashboardApi(
        this IServiceCollection services,
        DashboardApiOptions options)
    {
        services.AddSingleton(options);
        services.AddScoped<DashboardStatsService>();
        services.AddOutputCache(cacheOptions =>
        {
            cacheOptions.AddPolicy(ShortCachePolicyName, BuildDashboardCachePolicy(TimeSpan.FromSeconds(10)));
            cacheOptions.AddPolicy(SelectorCachePolicyName, BuildDashboardCachePolicy(TimeSpan.FromMinutes(1)));
        });

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
        })
        .CacheOutput(ShortCachePolicyName);

        api.MapGet("/global-overview", async (
            int? days,
            DateTime? startDate,
            DateTime? endDate,
            string? view,
            DashboardStatsService statsService,
            CancellationToken cancellationToken) =>
        {
            DashboardGlobalOverviewResponse overview = await statsService.GetGlobalOverviewAsync(
                days ?? 30,
                view,
                startDate,
                endDate,
                cancellationToken);

            return Results.Ok(overview);
        })
        .CacheOutput(ShortCachePolicyName);

        api.MapGet("/guilds", async (
            DashboardStatsService statsService,
            CancellationToken cancellationToken) =>
        {
            IReadOnlyList<DashboardGuildSummary> guilds = await statsService.GetGuildsAsync(cancellationToken);
            return Results.Ok(guilds);
        })
        .CacheOutput(SelectorCachePolicyName);

        api.MapGet("/guilds/{guildId:int}", async (
            int guildId,
            DashboardStatsService statsService,
            CancellationToken cancellationToken) =>
        {
            DashboardGuildSummary? guild = await statsService.GetGuildAsync(guildId, cancellationToken);
            return guild is null
                ? Results.NotFound(new { error = "Guild not found." })
                : Results.Ok(guild);
        })
        .CacheOutput(SelectorCachePolicyName);

        api.MapGet("/guild-options", async (
            DashboardStatsService statsService,
            CancellationToken cancellationToken) =>
        {
            IReadOnlyList<DashboardGuildSummary> guilds = await statsService.GetGuildOptionsAsync(cancellationToken);
            return Results.Ok(guilds);
        })
        .CacheOutput(SelectorCachePolicyName);

        api.MapGet("/activity", async (
            int? guildId,
            int? userId,
            string? channelId,
            int? days,
            DateTime? startDate,
            DateTime? endDate,
            DashboardStatsService statsService,
            CancellationToken cancellationToken) =>
        {
            if (HasInvalidDiscordId(channelId))
                return Results.BadRequest(new { error = "channelId must be a positive numeric Discord id." });

            DashboardActivitySeriesResponse series = await statsService.GetActivitySeriesAsync(
                guildId,
                days ?? 30,
                userId,
                channelId,
                startDate,
                endDate,
                cancellationToken);

            return Results.Ok(series);
        })
        .CacheOutput(ShortCachePolicyName);

        api.MapGet("/leaderboard", async (
            int? guildId,
            int? userId,
            string? channelId,
            string? metric,
            int? days,
            DateTime? startDate,
            DateTime? endDate,
            int? limit,
            DashboardStatsService statsService,
            CancellationToken cancellationToken) =>
        {
            if (HasInvalidDiscordId(channelId))
                return Results.BadRequest(new { error = "channelId must be a positive numeric Discord id." });

            DashboardLeaderboardResponse leaderboard = await statsService.GetActivityLeaderboardAsync(
                guildId,
                metric ?? "xp",
                days,
                limit ?? 10,
                userId,
                channelId,
                startDate,
                endDate,
                cancellationToken);

            return Results.Ok(leaderboard);
        })
        .CacheOutput(ShortCachePolicyName);

        api.MapGet("/insights", async (
            int? guildId,
            int? userId,
            string? channelId,
            int? days,
            string? scope,
            string? view,
            string? sortDirection,
            int? minActivity,
            DateTime? startDate,
            DateTime? endDate,
            DashboardStatsService statsService,
            CancellationToken cancellationToken) =>
        {
            if (HasInvalidDiscordId(channelId))
                return Results.BadRequest(new { error = "channelId must be a positive numeric Discord id." });

            DashboardInsightsResponse insights = await statsService.GetInsightsAsync(
                guildId,
                userId,
                channelId,
                days ?? 30,
                scope,
                sortDirection,
                minActivity,
                view,
                startDate,
                endDate,
                cancellationToken);

            return Results.Ok(insights);
        })
        .CacheOutput(ShortCachePolicyName);

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
        })
        .CacheOutput(ShortCachePolicyName);

        api.MapGet("/quotes/{quoteId:int}", async (
            int quoteId,
            DashboardStatsService statsService,
            CancellationToken cancellationToken) =>
        {
            DashboardQuoteDetailsResponse? quote = await statsService.GetQuoteDetailsAsync(quoteId, cancellationToken);

            return quote == null
                ? Results.NotFound()
                : Results.Ok(quote);
        })
        .CacheOutput(ShortCachePolicyName);

        api.MapGet("/quote-approvals/{approvalId:int}", async (
            int approvalId,
            DashboardStatsService statsService,
            CancellationToken cancellationToken) =>
        {
            DashboardQuoteApprovalRequestItem? approval = await statsService.GetQuoteApprovalDetailsAsync(approvalId, cancellationToken);

            return approval == null
                ? Results.NotFound()
                : Results.Ok(approval);
        })
        .CacheOutput(ShortCachePolicyName);

        return app;
    }

    private static Action<OutputCachePolicyBuilder> BuildDashboardCachePolicy(TimeSpan duration) =>
        policy => policy
            .Expire(duration)
            .SetVaryByQuery("*")
            .SetVaryByHeader("X-Dashboard-Key", "Origin");

    private static bool HasInvalidDiscordId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (!ulong.TryParse(value.Trim(), out ulong parsed) || parsed == 0UL);

    private static async ValueTask<object?> RequireDashboardApiKey(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        DashboardApiOptions options = context.HttpContext.RequestServices.GetRequiredService<DashboardApiOptions>();
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            return await InvokeNextAsync();

        if (context.HttpContext.Request.Headers.TryGetValue("X-Dashboard-Key", out var apiKey) &&
            string.Equals(apiKey.ToString(), options.ApiKey, StringComparison.Ordinal))
        {
            return await InvokeNextAsync();
        }

        return Results.Unauthorized();

        async ValueTask<object?> InvokeNextAsync()
        {
            try
            {
                return await next(context);
            }
            catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
            {
                return Results.StatusCode(499);
            }
        }
    }
}
