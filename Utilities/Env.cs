using System.Collections;
using System.Globalization;

namespace Morpheus.Utilities;

public class Env
{
    public static Dictionary<string, string> Variables { get; } = [];
    public static DateTime StartTime { get; } = DateTime.UtcNow;

    public static void Load(string filePath)
    {
        // Load variables from the .env file
        if (File.Exists(filePath))
        {
            string[] array = File.ReadAllLines(filePath);
            for (int i = 0; i < array.Length; i++)
            {
                string line = array[i];
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                    continue; // Skip empty lines and comments

                string[] parts = line.Split('=', 2);
                if (parts.Length != 2)
                    continue; // Skip lines that are not key-value pairs

                string key = parts[0].Trim();
                string value = parts[1].Trim();
                Environment.SetEnvironmentVariable(key, value);
                Variables.Add(key, value);
            }
        }

        // Add the remaining variables from the environment to the dictionary
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            string key = entry.Key.ToString() ?? string.Empty;
            if (!Variables.ContainsKey(key)) // .env variables take precedence
                Variables.Add(key, entry.Value?.ToString() ?? string.Empty);
        }
    }

    // Generic accessor
    // Usage: Env.Get<int>("PORT", 8080) or Env.Get("BOT_NAME", "Morpheus")
    public static T Get<T>(string key, T defaultValue = default!)
    {
        if (!Variables.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return defaultValue;

        var result = ConvertTo(raw, typeof(T));
        if (result is null)
            return defaultValue;
        return (T)result;
    }

    private static object? ConvertTo(string raw, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // strings
        if (underlying == typeof(string))
            return raw;

        // enums (case-insensitive)
        if (underlying.IsEnum)
        {
            if (Enum.TryParse(underlying, raw, ignoreCase: true, out var enumValue))
                return enumValue;
            return null;
        }

        // booleans (support 1/0, yes/no, on/off)
        if (underlying == typeof(bool))
        {
            var s = raw.Trim().ToLowerInvariant();
            if (s is "1" or "true" or "yes" or "y" or "on") return true;
            if (s is "0" or "false" or "no" or "n" or "off") return false;
            if (bool.TryParse(raw, out var b)) return b;
            return null;
        }

        // integral types
        if (underlying == typeof(int))
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i32) ? i32 : null;
        if (underlying == typeof(long))
            return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i64) ? i64 : null;
        if (underlying == typeof(short))
            return short.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i16) ? i16 : null;
        if (underlying == typeof(byte))
            return byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var u8) ? u8 : null;
        if (underlying == typeof(sbyte))
            return sbyte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s8) ? s8 : null;
        if (underlying == typeof(uint))
            return uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var u32) ? u32 : null;
        if (underlying == typeof(ulong))
            return ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var u64) ? u64 : null;
        if (underlying == typeof(ushort))
            return ushort.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var u16) ? u16 : null;

        // floating point / decimal
        if (underlying == typeof(double))
            return double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d) ? d : null;
        if (underlying == typeof(float))
            return float.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var f) ? f : null;
        if (underlying == typeof(decimal))
            return decimal.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var m) ? m : null;

        // dates/times
        if (underlying == typeof(DateTime))
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt) ? dt : null;
        if (underlying == typeof(TimeSpan))
            return TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var ts) ? ts : null;

        // common structured types
        if (underlying == typeof(Uri))
            return Uri.TryCreate(raw, UriKind.Absolute, out var uri) ? uri : null;
        if (underlying == typeof(Guid))
            return Guid.TryParse(raw, out var guid) ? guid : null;

        // last resort
        try
        {
            return Convert.ChangeType(raw, underlying, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }
}
