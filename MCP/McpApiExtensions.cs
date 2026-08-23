using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Morpheus.MCP;

public static class McpApiExtensions
{
    private const string CorsPolicyName = "McpCors";
    private const string RateLimitPolicyName = "McpRateLimit";

    public static IServiceCollection AddMcpApi(
        this IServiceCollection services,
        McpApiOptions options)
    {
        options.Validate();
        services.AddSingleton(options);

        if (!options.Enabled)
            return services;

        services.AddScoped<McpService>();

        services
            .AddMcpServer()
            .WithHttpTransport(transport => transport.Stateless = true)
            .WithTools<McpTools>();

        services.AddCors(corsOptions =>
        {
            corsOptions.AddPolicy(CorsPolicyName, policy =>
            {
                if (options.AllowedOrigins.Length > 0)
                {
                    policy
                        .WithOrigins(options.AllowedOrigins)
                        .WithMethods("GET", "POST", "DELETE")
                        .WithHeaders(
                            "Authorization",
                            "Content-Type",
                            "MCP-Protocol-Version",
                            "Mcp-Method",
                            "Mcp-Name",
                            "Mcp-Session-Id")
                        .WithExposedHeaders("WWW-Authenticate", "Mcp-Session-Id");
                }
            });
        });

        services.AddRateLimiter(rateLimitOptions =>
        {
            rateLimitOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            rateLimitOptions.AddPolicy(RateLimitPolicyName, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.RequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }

    public static WebApplication UseMcpApiSecurity(this WebApplication app)
    {
        McpApiOptions options = app.Services.GetRequiredService<McpApiOptions>();
        if (options.Enabled)
        {
            app.UseRateLimiter();
            app.UseMiddleware<McpSecurityMiddleware>();
        }

        return app;
    }

    public static WebApplication MapMcpApi(this WebApplication app)
    {
        McpApiOptions options = app.Services.GetRequiredService<McpApiOptions>();
        if (!options.Enabled)
            return app;

        app.MapMcp("/api/mcp")
            .RequireCors(CorsPolicyName)
            .RequireRateLimiting(RateLimitPolicyName);

        return app;
    }
}
