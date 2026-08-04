using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Morpheus.MCP;

/// <summary>
/// Enforces exact Origin validation and bearer-key authorization for every MCP request.
/// </summary>
public sealed class McpSecurityMiddleware(
    RequestDelegate next,
    McpApiOptions options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/mcp"))
        {
            await next(context);
            return;
        }

        if (context.Request.Headers.TryGetValue("Origin", out StringValues origins) &&
            (origins.Count != 1 || !options.IsAllowedOrigin(origins[0]!)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "The request origin is not allowed."
            });
            return;
        }

        string authorization = context.Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";
        if (!authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase) ||
            !KeysMatch(authorization[bearerPrefix.Length..].Trim(), options.ApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "A valid MCP bearer token is required."
            });
            return;
        }

        await next(context);
    }

    private static bool KeysMatch(string supplied, string expected)
    {
        byte[] suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        byte[] expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash);
    }
}
