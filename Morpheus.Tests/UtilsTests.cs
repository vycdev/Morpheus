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

    [Theory]
    [InlineData("(192.168.1.1)")]
    [InlineData("\"192.168.1.1\"")]
    [InlineData("See:192.168.1.1")]
    [InlineData("Visit 192.168.1.1.")]
    [InlineData("Connect to 192.168.1.1:8080/path")]
    public void ContainsUrl_DetectsIpv4AddressesAfterPunctuation(string text)
    {
        Assert.True(Utils.ContainsUrl(text));
    }

    [Theory]
    [InlineData("prefix192.168.1.1")]
    [InlineData("user@192.168.1.1")]
    [InlineData("user@[192.168.1.1]")]
    [InlineData("user@[IPv4:192.168.1.1]")]
    [InlineData("192.168.1.1.2")]
    [InlineData("192.168.1.1suffix")]
    [InlineData("192.168.1.1:abc")]
    [InlineData("192.168.1.1:123456")]
    [InlineData("192.168.1.1:80suffix")]
    public void ContainsUrl_DoesNotTreatEmbeddedIpv4AddressesAsUrls(string text)
    {
        Assert.False(Utils.ContainsUrl(text));
    }
}