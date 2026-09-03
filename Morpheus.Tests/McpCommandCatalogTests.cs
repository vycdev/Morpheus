using Discord.Commands;
using Discord.WebSocket;
using Morpheus.Attributes;
using Morpheus.Handlers;
using Morpheus.MCP;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Morpheus.Tests;

public class McpCommandCatalogTests
{
    [Fact]
    public async Task Manifest_CoversEveryVisibleRegistryCommandAndAlias()
    {
        CommandService commands = new();
        await commands.AddModulesAsync(typeof(McpTools).Assembly, new CatalogServiceProvider(commands));
        McpCommandCatalog catalog = new(commands);

        McpCommandManifest manifest = catalog.GetManifest();
        CommandInfo[] visible = [.. commands.Commands
            .Where(command => !command.Attributes.OfType<HiddenAttribute>().Any())];

        Assert.Equal(visible.Length, manifest.CommandCount);
        Assert.Equal(100, manifest.CoveragePercent);
        Assert.All(manifest.Commands, command =>
        {
            Assert.True(command.SupportsValidation);
            Assert.True(command.SupportsExecution);
        });

        foreach (CommandInfo command in visible)
        {
            foreach (string alias in command.Aliases)
            {
                McpCommandCapability capability = Assert.IsType<McpCommandCapability>(catalog.FindByAlias(alias));
                Assert.Contains(alias, capability.Aliases, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task Manifest_MatchesExplicitlyReviewedRegistryFingerprint()
    {
        CommandService commands = new();
        await commands.AddModulesAsync(typeof(McpTools).Assembly, new CatalogServiceProvider(commands));
        McpCommandCatalog catalog = new(commands);

        Assert.True(
            catalog.HasReviewedRegistry,
            $"The MCP registry changed. Review the new or changed commands, aliases, parameters, preconditions, and effects, then update the reviewed fingerprint to '{catalog.GetManifest().RegistryFingerprint}'.");
    }

    [Fact]
    public void Fingerprint_IncludesParameterDefaultMetadata()
    {
        McpCommandCapability withoutDefault = CreateFingerprintCapability(
            new McpCommandParameter("value", "String", true, false, false, false, string.Empty));
        McpCommandCapability withDefault = CreateFingerprintCapability(
            new McpCommandParameter("value", "String", false, false, false, true, "fallback"));

        Assert.NotEqual(
            McpCommandCatalog.ComputeFingerprint([withoutDefault]),
            McpCommandCatalog.ComputeFingerprint([withDefault]));
    }

    [Fact]
    public void Fingerprint_UsesPlatformIndependentLineEndings()
    {
        McpCommandCapability capability = CreateFingerprintCapability(
            new McpCommandParameter("value", "String", true, false, false, false, string.Empty));

        Assert.Equal(
            "cf2bad16a36dd31a36bf9d6551ca456a5493b8b50163ccffac6b76c0e3559475",
            McpCommandCatalog.ComputeFingerprint([capability]));
    }

    [Fact]
    public async Task Manifest_ExcludesHiddenOwnerCommands()
    {
        CommandService commands = new();
        await commands.AddModulesAsync(typeof(McpTools).Assembly, new CatalogServiceProvider(commands));
        McpCommandCatalog catalog = new(commands);

        Assert.Null(catalog.FindByAlias("dumplogs"));
        Assert.Null(catalog.FindByAlias("guildcount"));
        Assert.Null(catalog.FindByAlias("sendto"));
    }

    [Fact]
    public async Task Manifest_ReportsMaterialNetworkFileAndInteractionEffects()
    {
        CommandService commands = new();
        await commands.AddModulesAsync(typeof(McpTools).Assembly, new CatalogServiceProvider(commands));
        McpCommandCatalog catalog = new(commands);

        Assert.Contains("external-network", catalog.FindByAlias("advice")!.Effects);
        Assert.Contains("external-network", catalog.FindByAlias("subscribeyoutube")!.Effects);
        Assert.Contains("file-output", catalog.FindByAlias("qrcode")!.Effects);
        Assert.DoesNotContain("external-network", catalog.FindByAlias("qrcode")!.Effects);
        Assert.Contains("interactive-components", catalog.FindByAlias("reactroles")!.Effects);
    }

    [Fact]
    public async Task Manifest_CoversEveryVisibleModuleAndCachesTheHotPath()
    {
        CommandService commands = new();
        await commands.AddModulesAsync(typeof(McpTools).Assembly, new CatalogServiceProvider(commands));
        McpCommandCatalog catalog = new(commands);

        McpCommandManifest first = catalog.GetManifest();
        McpCommandManifest second = catalog.GetManifest();
        string[] expectedModules = [.. commands.Commands
            .Where(command => !command.Attributes.OfType<HiddenAttribute>().Any())
            .Select(command => command.Module.Name.Replace("Module", string.Empty, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)];

        Assert.Same(first, second);
        Assert.Equal(expectedModules, first.Commands.Select(command => command.Module).Distinct().Order());
        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int iteration = 0; iteration < 10_000; iteration++)
            Assert.NotNull(catalog.FindByAlias(first.Commands[iteration % first.CommandCount].Aliases[0]));
        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"10,000 cached alias lookups took {stopwatch.Elapsed}.");
    }

    private static McpCommandCapability CreateFingerprintCapability(McpCommandParameter parameter) =>
        new(
            "Test/command",
            "Test",
            "command",
            ["command"],
            "Test command.",
            [parameter],
            [],
            [],
            false,
            true,
            true);

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
