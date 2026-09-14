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

    [Theory]
    [InlineData("example.com:65536")]
    [InlineData("192.168.1.1:99999")]
    [InlineData("example.com:123456")]
    [InlineData("example.com:655350")]
    [InlineData("example.com:999999/path")]
    [InlineData("sub.example.com:123456/path")]
    [InlineData("example.com:abc")]
    [InlineData("example.com:80suffix")]
    [InlineData("example.com:+80")]
    [InlineData("example.com:-80")]
    [InlineData("example.com:１２３")]
    [InlineData("example.com:80:90")]
    [InlineData("example.com:65536/path")]
    [InlineData("192.168.1.1:65536/path")]
    public void ContainsUrl_RejectsInvalidPortsOnBareHosts(string text)
    {
        Assert.False(Utils.ContainsUrl(text));
    }

    [Theory]
    [InlineData("example.com:65535")]
    [InlineData("192.168.1.1:65535")]
    [InlineData("(example.com:65535)")]
    [InlineData("Visit example.com:65535.")]
    [InlineData("example.com:65535/path")]
    [InlineData("192.168.1.1:65535/path")]
    [InlineData("example.com:0/path")]
    [InlineData("192.168.1.1:0/path")]
    [InlineData("example.com:65536 then valid.example:8080")]
    [InlineData("192.168.1.1:99999 then 192.168.1.2:8080")]
    public void ContainsUrl_AcceptsPortBoundariesAndOtherValidHosts(string text)
    {
        Assert.True(Utils.ContainsUrl(text));
    }
}
