using System.Globalization;

namespace Morpheus.MCP;

internal static class McpDiscordId
{
    internal static ulong Parse(string value, string name) =>
        value is { Length: > 0 and <= 20 } &&
        value.All(character => character is >= '0' and <= '9') &&
        ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsed) &&
        parsed > 0
            ? parsed
            : throw new ArgumentException($"{name} must be a positive decimal Discord id.", name);

    internal static ulong? ParseOptional(string? value, string name) =>
        value is null ? null : Parse(value, name);
}
