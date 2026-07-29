using Morpheus.Utilities;

namespace Morpheus.Tests;

public class UtilsTests
{
    [Theory]
    [InlineData("(example.com)")]
    [InlineData("\"example.com\"")]
    [InlineData("See:example.com")]
    [InlineData("Visit example.com.")]
    public void ContainsUrl_DetectsBareDomainsAfterPunctuation(string text)
    {
        Assert.True(Utils.ContainsUrl(text));
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("user+example.com@example.org")]
    [InlineData("user%example.com@example.org")]
    [InlineData("user+example.com+tag@example.org")]
    [InlineData("user%example.com+tag@example.org")]
    [InlineData("user+example.com/path@example.org")]
    public void ContainsUrl_DoesNotTreatEmailAddressAsBareDomain(string text)
    {
        Assert.False(Utils.ContainsUrl(text));
    }
}