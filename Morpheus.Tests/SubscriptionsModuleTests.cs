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

    [Fact]
    public void EscapeLikePattern_EscapesWildcardsAndEscapeCharacters()
    {
        string escaped = SubscriptionsModule.EscapeLikePattern(@"https://example.com/feed?q=a%20_b\c");

        Assert.Equal(@"https://example.com/feed?q=a\%20\_b\\c", escaped);
    }
}
