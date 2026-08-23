using System.Security.Cryptography;
using System.Text;
using Discord.Commands;
using Morpheus.Attributes;

namespace Morpheus.MCP;

public sealed record McpCommandParameter(
    string Name,
    string Type,
    bool Required,
    bool Remainder,
    bool Multiple,
    string? DefaultValue);

public sealed record McpCommandCapability(
    string Id,
    string Module,
    string Name,
    IReadOnlyList<string> Aliases,
    string Summary,
    IReadOnlyList<McpCommandParameter> Parameters,
    IReadOnlyList<string> Preconditions,
    IReadOnlyList<string> Effects,
    bool RequiresGuild,
    bool SupportsValidation,
    bool SupportsExecution);

public sealed record McpCommandManifest(
    string RegistryFingerprint,
    int CommandCount,
    int AliasCount,
    int CoveragePercent,
    IReadOnlyList<McpCommandCapability> Commands);

/// <summary>
/// Cached, registry-derived description of every text command exposed to MCP.
/// The reviewed fingerprint deliberately changes whenever a command or alias changes.
/// </summary>
public sealed class McpCommandCatalog(CommandService commandService)
{
    // Update only after reviewing the MCP safety and context needs of every changed command.
    internal const string ReviewedRegistryFingerprint = "d70858444c51eaeee88102bcfb3c991c24c1bd5af6189504892bce3e44241ec9";

    private readonly object buildLock = new();
    private volatile McpCommandManifest? cachedManifest;
    private IReadOnlyDictionary<string, McpCommandCapability>? cachedAliases;

    public McpCommandManifest GetManifest()
    {
        if (cachedManifest is not null)
            return cachedManifest;

        lock (buildLock)
        {
            if (cachedManifest is not null)
                return cachedManifest;

            List<CommandInfo> registered = [.. commandService.Commands];
            if (registered.Count == 0)
                throw new InvalidOperationException("Morpheus commands have not finished registering yet.");

            List<McpCommandCapability> capabilities = [.. registered
                .Where(command => !command.Attributes.OfType<HiddenAttribute>().Any())
                .Select(CreateCapability)
                .OrderBy(command => command.Module, StringComparer.Ordinal)
                .ThenBy(command => command.Name, StringComparer.Ordinal)];

            Dictionary<string, McpCommandCapability> aliases = new(StringComparer.OrdinalIgnoreCase);
            foreach (McpCommandCapability capability in capabilities)
            {
                foreach (string alias in capability.Aliases)
                {
                    if (!aliases.TryAdd(alias, capability) && aliases[alias].Id != capability.Id)
                        throw new InvalidOperationException($"MCP command alias '{alias}' is ambiguous.");
                }
            }

            string fingerprint = ComputeFingerprint(capabilities);
            cachedAliases = aliases;
            cachedManifest = new McpCommandManifest(
                fingerprint,
                capabilities.Count,
                aliases.Count,
                capabilities.Count == 0 ? 0 : 100,
                capabilities);
            return cachedManifest;
        }
    }

    public McpCommandCapability? FindByAlias(string alias)
    {
        _ = GetManifest();
        return cachedAliases!.GetValueOrDefault(alias.Trim());
    }

    public bool HasReviewedRegistry =>
        string.Equals(GetManifest().RegistryFingerprint, ReviewedRegistryFingerprint, StringComparison.Ordinal);

    private static McpCommandCapability CreateCapability(CommandInfo command)
    {
        string module = command.Module.Name.EndsWith("Module", StringComparison.Ordinal)
            ? command.Module.Name[..^"Module".Length]
            : command.Module.Name;

        string[] aliases = [.. command.Aliases
            .Select(alias => alias.Trim())
            .Where(alias => alias.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(alias => alias, StringComparer.Ordinal)];
        string primaryAlias = command.Aliases[0];

        bool requiresGuild = command.Preconditions
            .OfType<RequireContextAttribute>()
            .Any(attribute => attribute.Contexts.HasFlag(ContextType.Guild));

        string[] preconditions = [.. command.Module.Preconditions
            .Concat(command.Preconditions)
            .Select(attribute => attribute.GetType().Name.Replace("Attribute", string.Empty, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)];

        McpCommandParameter[] parameters = [.. command.Parameters.Select(parameter =>
            new McpCommandParameter(
                parameter.Name,
                GetFriendlyTypeName(parameter.Type),
                !parameter.IsOptional,
                parameter.IsRemainder,
                parameter.IsMultiple,
                parameter.DefaultValue?.ToString()))];

        return new McpCommandCapability(
            $"{module}/{primaryAlias}",
            module,
            primaryAlias,
            aliases,
            command.Summary ?? "No description available.",
            parameters,
            preconditions,
            ClassifyEffects(module, primaryAlias),
            requiresGuild,
            SupportsValidation: true,
            SupportsExecution: true);
    }

    private static IReadOnlyList<string> ClassifyEffects(string module, string command)
    {
        HashSet<string> effects = new(StringComparer.Ordinal) { "captured-response" };

        if (module is "Button" or "Economy" or "Guild" or "Quotes" or "ReactionRoles" or "Slots" or "Stocks" or "Subscriptions" or "Utility" or "ActivityRoles")
            effects.Add("database-write");
        if (module == "Levels" && command == "invalidatexp")
            effects.Add("database-write");
        if (module is "ActivityRoles" or "Emojis" or "Guild" or "Quotes" or "ReactionRoles" or "Subscriptions" or "Utility")
            effects.Add("discord-write");
        if (module == "Subscriptions" ||
            module == "Emojis" && command is "downloademojis" or "importemoji" ||
            module == "Image" && command is "cat" or "dog" or "deepfry" or "deepfryextra" or "memefy" or "blurpify" ||
            command is "advice" or "fact" or "joke" or "pingmc" or "udic")
            effects.Add("external-network");
        if (module == "Image" && command is "deepfry" or "deepfryextra" or "memefy" or "captcha" or "qrcode" or "blurpify" ||
            command.Contains("graph", StringComparison.OrdinalIgnoreCase) ||
            command == "downloademojis")
            effects.Add("file-output");
        if (command is "help" or "importemoji" or "reactroles" or "slots" or "subscriptions")
            effects.Add("interactive-components");

        return [.. effects.OrderBy(effect => effect, StringComparer.Ordinal)];
    }

    private static string GetFriendlyTypeName(Type type)
    {
        Type? nullable = Nullable.GetUnderlyingType(type);
        if (nullable is not null)
            return GetFriendlyTypeName(nullable) + "?";
        if (type.IsArray)
            return GetFriendlyTypeName(type.GetElementType()!) + "[]";
        if (!type.IsGenericType)
            return type.Name;

        string name = type.Name[..type.Name.IndexOf('`')];
        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName))}>";
    }

    private static string ComputeFingerprint(IEnumerable<McpCommandCapability> capabilities)
    {
        StringBuilder canonical = new();
        foreach (McpCommandCapability command in capabilities)
        {
            canonical.Append(command.Id).Append('|')
                .AppendJoin(',', command.Aliases).Append('|')
                .AppendJoin(',', command.Parameters.Select(parameter =>
                    $"{parameter.Name}:{parameter.Type}:{parameter.Required}:{parameter.Remainder}:{parameter.Multiple}"))
                .Append('|').AppendJoin(',', command.Preconditions)
                .Append('|').AppendJoin(',', command.Effects)
                .AppendLine();
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }
}
