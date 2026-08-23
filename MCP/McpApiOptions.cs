using Morpheus.Utilities;

namespace Morpheus.MCP;

/// <summary>
/// Security and rate-limit configuration for the MCP endpoint.
/// The MCP server is disabled unless an API key is configured.
/// </summary>
public sealed record McpApiOptions(
    string[] AllowedOrigins,
    string ApiKey,
    int RequestsPerMinute)
{
    public const int DefaultRequestsPerMinute = 60;
    public const string DefaultListenerUrls = "http://127.0.0.1:5268";

    public string ListenerUrls { get; init; } = DefaultListenerUrls;

    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);

    public static McpApiOptions FromEnvironment()
    {
        string configuredOrigins = Env.Get(
            "MCP_ALLOWED_ORIGINS",
            "http://localhost:3000,http://127.0.0.1:3000");

        string[] origins = [.. configuredOrigins
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeOrigin)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

        return new McpApiOptions(
            origins,
            Env.Get("MCP_API_KEY", string.Empty),
            Env.Get("MCP_RATE_LIMIT_PER_MINUTE", DefaultRequestsPerMinute))
        {
            ListenerUrls = Env.Get("MCP_API_URLS", DefaultListenerUrls)
        };
    }

    public void Validate()
    {
        if (!Enabled)
            return;

        if (RequestsPerMinute <= 0)
            throw new InvalidOperationException("MCP_RATE_LIMIT_PER_MINUTE must be greater than zero.");

        foreach (string origin in AllowedOrigins)
            _ = NormalizeOrigin(origin);
    }

    public bool IsAllowedOrigin(string origin)
    {
        string normalized;
        try
        {
            normalized = NormalizeOrigin(origin);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return AllowedOrigins.Any(allowed => string.Equals(
            NormalizeOrigin(allowed),
            normalized,
            StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizeOrigin(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            (uri.AbsolutePath != "/" && !string.IsNullOrEmpty(uri.AbsolutePath)) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                $"Invalid MCP origin '{value}'. Origins must contain only an http(s) scheme, host, and optional port.");
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }
}
