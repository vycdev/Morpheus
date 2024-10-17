namespace Morpheus.Utilities;
public class Env
{
    public static Dictionary<string, string> Variables { get; } = new();

    public static void Load(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"The file '{filePath}' does not exist.");

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
}
