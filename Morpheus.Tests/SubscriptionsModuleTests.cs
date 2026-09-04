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

    [Theory]
    [InlineData("@Streamer", "streamer")]
    [InlineData("twitch.tv/Streamer", "streamer")]
    [InlineData("www.twitch.tv/Streamer/videos", "streamer")]
    [InlineData("https://m.twitch.tv/Streamer?referrer=raid", "streamer")]
    public void ExtractTwitchLogin_AcceptsHandlesAndTwitchUrls(string input, string expected)
    {
        Assert.Equal(expected, SubscriptionsModule.ExtractTwitchLogin(input));
    }

    [Theory]
    [InlineData("https://example.com/twitch.tv/Streamer")]
    [InlineData("https://not-twitch.tv/Streamer")]
    [InlineData("example.com/twitch.tv/Streamer")]
    [InlineData("https://twitch.tv.evil.example/Streamer")]
    [InlineData("example.com/Streamer")]
    [InlineData("twitch.tv.evil.example/Streamer")]
    public void ExtractTwitchLogin_RejectsNonTwitchUrls(string input)
    {
        Assert.Empty(SubscriptionsModule.ExtractTwitchLogin(input));
    }

    [Fact]
    public void EscapeLikePattern_EscapesWildcardsAndEscapeCharacters()
    {
        string escaped = SubscriptionsModule.EscapeLikePattern(@"https://example.com/feed?q=a%20_b\c");

        Assert.Equal(@"https://example.com/feed?q=a\%20\_b\\c", escaped);
    }

    [Fact]
    public void EscapeBrowserText_DoesNotSplitSurrogatePairsWhenTruncated()
    {
        string value = new string('a', 78) + "😀tail";

        string result = SubscriptionsModule.EscapeBrowserText(value, 80);

        Assert.Equal(new string('a', 78) + "…", result);
        Assert.DoesNotContain(result, char.IsSurrogate);
    }
}
