using Morpheus.Utilities;

namespace Morpheus.Dashboard;

public sealed record DashboardApiOptions(
    string Urls,
    string[] CorsOrigins,
    string ApiKey,
    int MaxActivityDays)
{
    public const int DefaultMaxActivityDays = 3650;

    public static DashboardApiOptions FromEnvironment()
    {
        string urls = Env.Get("DASHBOARD_API_URLS", "http://127.0.0.1:5267");
        string corsOrigins = Env.Get("DASHBOARD_CORS_ORIGINS", "http://localhost:3000,http://127.0.0.1:3000");

        string[] origins = [.. corsOrigins
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        return new DashboardApiOptions(
            urls,
            origins,
            Env.Get("DASHBOARD_API_KEY", string.Empty),
            Env.Get("ACTIVITY_GRAPHS_MAX_DAYS", DefaultMaxActivityDays));
    }
}
