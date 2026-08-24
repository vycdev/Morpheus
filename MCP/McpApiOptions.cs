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
    public const int DefaultCommandTimeoutSeconds = 45;
    public const int DefaultMaxConcurrentCommands = 4;
    public const int DefaultMaxCommandLength = 2000;
    public const int DefaultMaxAttachments = 10;
    public const long DefaultMaxAttachmentBytes = 8 * 1024 * 1024;
    public const int DefaultMaxCapturedOutputBytes = 2 * 1024 * 1024;
    public const int DefaultMaxCapturedOutputs = 100;
    public const int DefaultIdempotencyMinutes = 10;

    public string ListenerUrls { get; init; } = DefaultListenerUrls;
    public bool CommandExecutionEnabled { get; init; }
    public int CommandTimeoutSeconds { get; init; } = DefaultCommandTimeoutSeconds;
    public int MaxConcurrentCommands { get; init; } = DefaultMaxConcurrentCommands;
    public int MaxCommandLength { get; init; } = DefaultMaxCommandLength;
    public int MaxAttachments { get; init; } = DefaultMaxAttachments;
    public long MaxAttachmentBytes { get; init; } = DefaultMaxAttachmentBytes;
    public int MaxCapturedOutputBytes { get; init; } = DefaultMaxCapturedOutputBytes;
    public int MaxCapturedOutputs { get; init; } = DefaultMaxCapturedOutputs;
    public int IdempotencyMinutes { get; init; } = DefaultIdempotencyMinutes;

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
            ListenerUrls = Env.Get("MCP_API_URLS", DefaultListenerUrls),
            CommandExecutionEnabled = Env.Get("MCP_COMMAND_EXECUTION_ENABLED", false),
            CommandTimeoutSeconds = Env.Get("MCP_COMMAND_TIMEOUT_SECONDS", DefaultCommandTimeoutSeconds),
            MaxConcurrentCommands = Env.Get("MCP_MAX_CONCURRENT_COMMANDS", DefaultMaxConcurrentCommands),
            MaxCommandLength = Env.Get("MCP_MAX_COMMAND_LENGTH", DefaultMaxCommandLength),
            MaxAttachments = Env.Get("MCP_MAX_ATTACHMENTS", DefaultMaxAttachments),
            MaxAttachmentBytes = Env.Get("MCP_MAX_ATTACHMENT_BYTES", DefaultMaxAttachmentBytes),
            MaxCapturedOutputBytes = Env.Get("MCP_MAX_CAPTURED_OUTPUT_BYTES", DefaultMaxCapturedOutputBytes),
            MaxCapturedOutputs = Env.Get("MCP_MAX_CAPTURED_OUTPUTS", DefaultMaxCapturedOutputs),
            IdempotencyMinutes = Env.Get("MCP_IDEMPOTENCY_MINUTES", DefaultIdempotencyMinutes)
        };
    }

    public void Validate()
    {
        if (!Enabled)
            return;

        if (RequestsPerMinute <= 0)
            throw new InvalidOperationException("MCP_RATE_LIMIT_PER_MINUTE must be greater than zero.");
        if (CommandTimeoutSeconds is < 1 or > 300)
            throw new InvalidOperationException("MCP_COMMAND_TIMEOUT_SECONDS must be between 1 and 300.");
        if (MaxConcurrentCommands is < 1 or > 32)
            throw new InvalidOperationException("MCP_MAX_CONCURRENT_COMMANDS must be between 1 and 32.");
        if (MaxCommandLength is < 1 or > 4000)
            throw new InvalidOperationException("MCP_MAX_COMMAND_LENGTH must be between 1 and 4000.");
        if (MaxAttachments is < 0 or > 10)
            throw new InvalidOperationException("MCP_MAX_ATTACHMENTS must be between 0 and 10.");
        if (MaxAttachmentBytes is < 1 or > 25 * 1024 * 1024)
            throw new InvalidOperationException("MCP_MAX_ATTACHMENT_BYTES must be between 1 byte and 25 MiB.");
        if (MaxCapturedOutputBytes is < 1 or > 8 * 1024 * 1024)
            throw new InvalidOperationException("MCP_MAX_CAPTURED_OUTPUT_BYTES must be between 1 byte and 8 MiB.");
        if (MaxCapturedOutputs is < 1 or > 500)
            throw new InvalidOperationException("MCP_MAX_CAPTURED_OUTPUTS must be between 1 and 500.");
        if (IdempotencyMinutes is < 1 or > 1440)
            throw new InvalidOperationException("MCP_IDEMPOTENCY_MINUTES must be between 1 and 1440.");

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
