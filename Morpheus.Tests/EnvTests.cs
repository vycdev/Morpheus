using Morpheus.Utilities;

namespace Morpheus.Tests;

[CollectionDefinition("Environment variable tests", DisableParallelization = true)]
public class EnvironmentVariableTestCollection
{
}

[Collection("Environment variable tests")]
public class EnvTests
{
    [Fact]
    public void Load_DuplicateKeys_UsesLastValue()
    {
        string key = $"MORPHEUS_ENV_TEST_{Guid.NewGuid():N}";
        string? originalEnvironmentValue = Environment.GetEnvironmentVariable(key);
        Dictionary<string, string> originalVariables = new(Env.Variables);
        string filePath = Path.GetTempFileName();

        try
        {
            File.WriteAllLines(filePath, [$"{key}=first", $"{key}=second"]);

            Env.Load(filePath);

            Assert.Equal("second", Env.Variables[key]);
            Assert.Equal("second", Environment.GetEnvironmentVariable(key));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, originalEnvironmentValue);
            Env.Variables.Clear();
            foreach ((string existingKey, string existingValue) in originalVariables)
                Env.Variables[existingKey] = existingValue;
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Load_EmptyKey_SkipsMalformedLine()
    {
        string key = $"MORPHEUS_ENV_TEST_{Guid.NewGuid():N}";
        string? originalEnvironmentValue = Environment.GetEnvironmentVariable(key);
        Dictionary<string, string> originalVariables = new(Env.Variables);
        string filePath = Path.GetTempFileName();

        try
        {
            File.WriteAllLines(filePath, ["=malformed", $"{key}=valid"]);

            Env.Load(filePath);

            Assert.Equal("valid", Env.Variables[key]);
            Assert.Equal("valid", Environment.GetEnvironmentVariable(key));
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, originalEnvironmentValue);
            Env.Variables.Clear();
            foreach ((string existingKey, string existingValue) in originalVariables)
                Env.Variables[existingKey] = existingValue;
            File.Delete(filePath);
        }
    }
}
