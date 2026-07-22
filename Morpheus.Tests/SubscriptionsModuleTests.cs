using Morpheus.Modules;

namespace Morpheus.Tests;

public class SubscriptionsModuleTests
{
    [Fact]
    public void ExtractTwitchLogin_StripsUrlFragment()
    {
        string login = SubscriptionsModule.ExtractTwitchLogin("https://twitch.tv/Streamer#about");

        Assert.Equal("streamer", login);
    }
}
