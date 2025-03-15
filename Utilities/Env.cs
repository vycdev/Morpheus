using System.Collections;

namespace Morpheus.Utilities;
public class Env
{
    public static Dictionary<string, string> Variables { get; } = new();
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
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
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

    public static string Get(string key, string defaultValue = "")
        =>  Variables.TryGetValue(key, out string? value) ? value : defaultValue;
}
