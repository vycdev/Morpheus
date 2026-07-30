using Morpheus.Utilities;

namespace Morpheus.MCP;

/// <summary>
/// Configuration options for the MCP API server.
/// </summary>
public sealed record McpApiOptions(
    string Urls,
    string ApiKey)
{
    public static McpApiOptions FromEnvironment()
    {
        string urls = Env.Get("MCP_API_URLS", "http://127.0.0.1:5268");
        string apiKey = Env.Get("MCP_API_KEY", string.Empty);
        return new McpApiOptions(urls, apiKey);
    }
}