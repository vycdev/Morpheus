using Morpheus.Handlers;
using Morpheus.Utilities;

namespace Morpheus.Tests;

[Collection("Environment variable tests")]
public class WelcomeHandlerTests
{
    [Fact]
    public void GetCustomEmoteId_WhenSettingIsMissing_ReturnsNull()
    {
        const string key = "CUSTOM_JOIN_EMOTE_ID";
        bool hadOriginalValue = Env.Variables.TryGetValue(key, out string? originalValue);

        try
        {
            Env.Variables.Remove(key);

            Assert.Null(WelcomeHandler.GetCustomEmoteId(key));
        }
        finally
        {
            if (hadOriginalValue)
                Env.Variables[key] = originalValue!;
            else
                Env.Variables.Remove(key);
        }
    }
}
