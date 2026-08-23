using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Morpheus.Database;
using Morpheus.Database.Models;
using Morpheus.Handlers;
using Morpheus.MCP;

namespace Morpheus.Tests;

public class McpApiEndpointTests
{
    private const string ApiKey = "integration-test-mcp-key";
    private const string AllowedOrigin = "https://client.example";

    [Fact]
    public async Task Endpoint_RequiresValidBearerCredentials()
    {
        await using McpTestServer server = await McpTestServer.CreateAsync();

        using HttpRequestMessage missing = CreateInitializeRequest();
        using HttpResponseMessage missingResponse = await server.Client.SendAsync(missing);
        Assert.Equal(HttpStatusCode.Unauthorized, missingResponse.StatusCode);
        Assert.Equal("Bearer", missingResponse.Headers.WwwAuthenticate.Single().Scheme);

        using HttpRequestMessage incorrect = CreateInitializeRequest("wrong-key");
        using HttpResponseMessage incorrectResponse = await server.Client.SendAsync(incorrect);
        Assert.Equal(HttpStatusCode.Unauthorized, incorrectResponse.StatusCode);

        using HttpRequestMessage correct = CreateInitializeRequest(ApiKey);
        using HttpResponseMessage correctResponse = await server.Client.SendAsync(correct);
        Assert.Equal(HttpStatusCode.OK, correctResponse.StatusCode);
    }

    [Fact]
    public async Task Endpoint_RejectsUnlistedOriginAndAcceptsAllowedOrigin()
    {
        await using McpTestServer server = await McpTestServer.CreateAsync();

        using HttpRequestMessage rejected = CreateInitializeRequest(ApiKey, "https://evil.example");
        using HttpResponseMessage rejectedResponse = await server.Client.SendAsync(rejected);
        Assert.Equal(HttpStatusCode.Forbidden, rejectedResponse.StatusCode);

        using HttpRequestMessage allowed = CreateInitializeRequest(ApiKey, AllowedOrigin);
        using HttpResponseMessage allowedResponse = await server.Client.SendAsync(allowed);
        Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
        Assert.Equal(AllowedOrigin, allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Endpoint_ImplementsInitializeAndToolsList()
    {
        await using McpTestServer server = await McpTestServer.CreateAsync();

        using HttpRequestMessage initialize = CreateInitializeRequest(ApiKey);
        using HttpResponseMessage initializeResponse = await server.Client.SendAsync(initialize);
        JsonDocument initializeJson = await ReadJsonAsync(initializeResponse);
        Assert.Equal("2.0", initializeJson.RootElement.GetProperty("jsonrpc").GetString());
        Assert.Equal(1, initializeJson.RootElement.GetProperty("id").GetInt32());
        Assert.True(initializeJson.RootElement.GetProperty("result").TryGetProperty("serverInfo", out _));

        using HttpRequestMessage listTools = CreateJsonRpcRequest(
            2,
            "tools/list",
            "{}",
            ApiKey);
        using HttpResponseMessage listResponse = await server.Client.SendAsync(listTools);
        JsonDocument listJson = await ReadJsonAsync(listResponse);

        JsonElement tools = listJson.RootElement
            .GetProperty("result")
            .GetProperty("tools");
        string[] names = [.. tools.EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString()!)];

        Assert.Contains("get_activity_overview", names);
        Assert.Contains("get_approved_quotes", names);
        Assert.DoesNotContain("get_guild_leaderboard", names);
        Assert.Contains("list_commands", names);
        Assert.Contains("describe_command", names);
        Assert.Contains("run_command", names);
        Assert.DoesNotContain("get_recent_logs", names);
        Assert.DoesNotContain("get_users", names);
        Assert.All(tools.EnumerateArray(), tool =>
            Assert.Equal(JsonValueKind.Object, tool.GetProperty("inputSchema").ValueKind));
    }

    [Fact]
    public async Task Endpoint_ExecutesStandardToolsCall()
    {
        await using McpTestServer server = await McpTestServer.CreateAsync();

        using HttpRequestMessage call = CreateJsonRpcRequest(
            3,
            "tools/call",
            """{"name":"get_activity_overview","arguments":{}}""",
            ApiKey);
        using HttpResponseMessage response = await server.Client.SendAsync(call);
        JsonDocument json = await ReadJsonAsync(response);

        Assert.Equal(3, json.RootElement.GetProperty("id").GetInt32());
        JsonElement result = json.RootElement.GetProperty("result");
        if (result.TryGetProperty("isError", out JsonElement isError))
            Assert.False(isError.GetBoolean());
        Assert.True(
            result.TryGetProperty("structuredContent", out JsonElement structured),
            result.GetRawText());
        Assert.Equal(0, structured.GetProperty("totalMessages").GetInt64());
    }

    [Fact]
    public async Task ListCommands_OutputMatchesAdvertisedParameterSchema()
    {
        await using McpTestServer server = await McpTestServer.CreateAsync();
        await server.RegisterCommandsAsync();

        using HttpRequestMessage call = CreateJsonRpcRequest(
            4,
            "tools/call",
            """{"name":"list_commands","arguments":{}}""",
            ApiKey);
        using HttpResponseMessage response = await server.Client.SendAsync(call);
        JsonDocument json = await ReadJsonAsync(response);

        JsonElement result = json.RootElement.GetProperty("result");
        if (result.TryGetProperty("isError", out JsonElement isError))
            Assert.False(isError.GetBoolean(), result.GetRawText());

        JsonElement commands = result
            .GetProperty("structuredContent")
            .GetProperty("commands");
        JsonElement[] parameters = [.. commands.EnumerateArray()
            .SelectMany(command => command.GetProperty("parameters").EnumerateArray())];

        Assert.NotEmpty(parameters);
        Assert.All(parameters, parameter =>
        {
            Assert.True(parameter.TryGetProperty("hasDefaultValue", out _), parameter.GetRawText());
            Assert.Equal(JsonValueKind.String, parameter.GetProperty("defaultValue").ValueKind);
        });
        Assert.Contains(parameters, parameter =>
            !parameter.GetProperty("hasDefaultValue").GetBoolean() &&
            parameter.GetProperty("defaultValue").GetString() == string.Empty);
    }

    [Fact]
    public async Task RunCommand_OutputIncludesNullableFieldsRequiredBySchema()
    {
        await using McpTestServer server = await McpTestServer.CreateAsync();
        await server.RegisterCommandsAsync();

        using HttpRequestMessage call = CreateJsonRpcRequest(
            5,
            "tools/call",
            """{"name":"run_command","arguments":{"invocation":{"command":"a","userId":"123","channelId":"456","mode":"validate"}}}""",
            ApiKey);
        using HttpResponseMessage response = await server.Client.SendAsync(call);
        JsonDocument json = await ReadJsonAsync(response);

        JsonElement result = json.RootElement.GetProperty("result");
        if (result.TryGetProperty("isError", out JsonElement isError))
            Assert.False(isError.GetBoolean(), result.GetRawText());

        JsonElement structured = result.GetProperty("structuredContent");
        Assert.Equal(JsonValueKind.Null, structured.GetProperty("command").ValueKind);
        Assert.True(structured.TryGetProperty("error", out _), structured.GetRawText());
        Assert.True(structured.TryGetProperty("errorReason", out _), structured.GetRawText());
    }

    [Fact]
    public async Task ApprovedQuotesTool_NeverReturnsPendingQuotes()
    {
        await using McpTestServer server = await McpTestServer.CreateAsync();
        int guildId = await server.SeedApprovedAndPendingQuotesAsync();

        string parameters = JsonSerializer.Serialize(new
        {
            name = "get_approved_quotes",
            arguments = new { guildId }
        });
        using HttpRequestMessage call = CreateJsonRpcRequest(
            4,
            "tools/call",
            parameters,
            ApiKey);
        using HttpResponseMessage response = await server.Client.SendAsync(call);
        JsonDocument json = await ReadJsonAsync(response);

        JsonElement result = json.RootElement.GetProperty("result");
        Assert.True(
            result.TryGetProperty("structuredContent", out JsonElement structured),
            result.GetRawText());
        JsonElement item = Assert.Single(structured.GetProperty("items").EnumerateArray());
        Assert.Equal("approved content", item.GetProperty("content").GetString());
        Assert.Equal(1, structured.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Endpoint_ReturnsTooManyRequestsAfterConfiguredLimit()
    {
        await using McpTestServer server = await McpTestServer.CreateAsync(requestsPerMinute: 2);

        for (int id = 1; id <= 2; id++)
        {
            using HttpRequestMessage allowed = CreateInitializeRequest(ApiKey, id: id);
            using HttpResponseMessage allowedResponse = await server.Client.SendAsync(allowed);
            Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
        }

        using HttpRequestMessage rejected = CreateInitializeRequest(ApiKey, id: 3);
        using HttpResponseMessage rejectedResponse = await server.Client.SendAsync(rejected);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
    }

    [Fact]
    public async Task Endpoint_RateLimitsInvalidCredentials()
    {
        await using McpTestServer server = await McpTestServer.CreateAsync(requestsPerMinute: 2);

        for (int id = 1; id <= 2; id++)
        {
            using HttpRequestMessage invalid = CreateInitializeRequest("wrong-key", id: id);
            using HttpResponseMessage invalidResponse = await server.Client.SendAsync(invalid);
            Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);
        }

        using HttpRequestMessage rejected = CreateInitializeRequest("wrong-key", id: 3);
        using HttpResponseMessage rejectedResponse = await server.Client.SendAsync(rejected);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
    }

    [Fact]
    public async Task CorsPreflight_AllowsCurrentMcpHeadersFromConfiguredOrigin()
    {
        await using McpTestServer server = await McpTestServer.CreateAsync();
        using HttpRequestMessage preflight = new(HttpMethod.Options, "/api/mcp");
        preflight.Headers.Add("Origin", AllowedOrigin);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add(
            "Access-Control-Request-Headers",
            "authorization,content-type,mcp-protocol-version,mcp-method,mcp-name");

        using HttpResponseMessage response = await server.Client.SendAsync(preflight);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        string allowedHeaders = response.Headers.GetValues("Access-Control-Allow-Headers").Single();
        Assert.Contains("mcp-method", allowedHeaders, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mcp-name", allowedHeaders, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Endpoint_AcceptsCurrentProtocolRequestHeaders()
    {
        await using McpTestServer server = await McpTestServer.CreateAsync();
        using HttpRequestMessage request = CreateJsonRpcRequest(
            5,
            "tools/list",
            """{"_meta":{"io.modelcontextprotocol/protocolVersion":"2026-07-28","io.modelcontextprotocol/clientCapabilities":{}}}""",
            ApiKey);
        request.Headers.Add("MCP-Protocol-Version", "2026-07-28");
        request.Headers.Add("Mcp-Method", "tools/list");

        using HttpResponseMessage response = await server.Client.SendAsync(request);
        JsonDocument json = await ReadJsonAsync(response);

        Assert.Equal(5, json.RootElement.GetProperty("id").GetInt32());
        Assert.True(json.RootElement.GetProperty("result").TryGetProperty("tools", out _));
    }

    [Fact]
    public void Options_RejectMalformedOriginsAndNonPositiveRateLimit()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new McpApiOptions(["https://client.example/path"], ApiKey, 60).Validate());
        Assert.Throws<InvalidOperationException>(() =>
            new McpApiOptions([AllowedOrigin], ApiKey, 0).Validate());
        Assert.Throws<InvalidOperationException>(() =>
            new McpApiOptions([AllowedOrigin], ApiKey, 60) { CommandTimeoutSeconds = 0 }.Validate());
        Assert.Throws<InvalidOperationException>(() =>
            new McpApiOptions([AllowedOrigin], ApiKey, 60) { MaxConcurrentCommands = 0 }.Validate());
        Assert.Throws<InvalidOperationException>(() =>
            new McpApiOptions([AllowedOrigin], ApiKey, 60) { MaxCapturedOutputs = 0 }.Validate());
    }

    [Fact]
    public void Options_DefaultListenerIsLoopbackOnly()
    {
        McpApiOptions options = new([], string.Empty, 60);

        Assert.Equal("http://127.0.0.1:5268", options.ListenerUrls);
    }

    private static HttpRequestMessage CreateInitializeRequest(
        string? apiKey = null,
        string? origin = null,
        int id = 1) =>
        CreateJsonRpcRequest(
            id,
            "initialize",
            """{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"Morpheus.Tests","version":"1.0"}}""",
            apiKey,
            origin);

    private static HttpRequestMessage CreateJsonRpcRequest(
        int id,
        string method,
        string parameters,
        string? apiKey,
        string? origin = null)
    {
        HttpRequestMessage request = new(HttpMethod.Post, "/api/mcp")
        {
            Content = new StringContent(
                $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":{JsonSerializer.Serialize(method)},\"params\":{parameters}}}",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (apiKey is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        if (origin is not null)
            request.Headers.Add("Origin", origin);
        return request;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        string content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Unexpected {(int)response.StatusCode}: {content}");
        string payload = content.StartsWith("event:", StringComparison.Ordinal)
            ? content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Single(line => line.StartsWith("data:", StringComparison.Ordinal))[5..]
                .Trim()
            : content;
        return JsonDocument.Parse(payload);
    }

    private sealed class McpTestServer(
        SqliteConnection connection,
        WebApplication app,
        HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public async Task RegisterCommandsAsync()
        {
            CommandService commands = app.Services.GetRequiredService<CommandService>();
            await commands.AddModulesAsync(
                typeof(McpTools).Assembly,
                new CatalogServiceProvider(commands));
        }

        public async Task<int> SeedApprovedAndPendingQuotesAsync()
        {
            await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
            DB db = scope.ServiceProvider.GetRequiredService<DB>();
            Guild guild = new() { DiscordId = 123, Name = "MCP Test Guild" };
            User user = new() { DiscordId = 456, Username = "MCP Test User" };
            db.Guilds.Add(guild);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.Quotes.AddRange(
                new Quote
                {
                    GuildId = guild.Id,
                    UserId = user.Id,
                    Content = "approved content",
                    Approved = true
                },
                new Quote
                {
                    GuildId = guild.Id,
                    UserId = user.Id,
                    Content = "pending content",
                    Approved = false
                });
            await db.SaveChangesAsync();
            return guild.Id;
        }

        public static async Task<McpTestServer> CreateAsync(int requestsPerMinute = 60)
        {
            SqliteConnection connection = new("DataSource=:memory:");
            await connection.OpenAsync();

            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = "Testing",
                ApplicationName = typeof(McpApiExtensions).Assembly.GetName().Name
            });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddDbContext<DB>(options => options.UseSqlite(connection));
            builder.Services.AddMcpApi(new McpApiOptions(
                [AllowedOrigin],
                ApiKey,
                requestsPerMinute));

            WebApplication app = builder.Build();
            app.UseCors();
            app.UseMcpApiSecurity();
            app.MapMcpApi();

            await app.StartAsync();
            await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
            {
                DB db = scope.ServiceProvider.GetRequiredService<DB>();
                await db.Database.EnsureCreatedAsync();
            }

            return new McpTestServer(connection, app, app.GetTestClient());
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class CatalogServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> instances;

        public CatalogServiceProvider(CommandService commands)
        {
            DiscordSocketClient client = new();
            instances = new Dictionary<Type, object>
            {
                [typeof(CommandService)] = commands,
                [typeof(DiscordSocketClient)] = client,
                [typeof(InteractionsHandler)] = new InteractionsHandler(client)
            };
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IServiceProvider))
                return this;
            if (instances.TryGetValue(serviceType, out object? instance))
                return instance;
            if (serviceType.IsClass)
            {
                instance = RuntimeHelpers.GetUninitializedObject(serviceType);
                instances[serviceType] = instance;
                return instance;
            }

            return null;
        }
    }
}
