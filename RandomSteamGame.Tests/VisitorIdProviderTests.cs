using RandomSteamGame.Options;
using RandomSteamGame.Services;

namespace RandomSteamGame.Tests;

public sealed class VisitorIdProviderTests
{
    private const string TestKey =
        "test-visitor-hash-key-that-is-not-a-production-secret";

    [Fact]
    public void GetVisitorId_SameAddress_ReturnsSameId()
    {
        var provider = CreateProvider();

        var first = provider.GetVisitorId("192.0.2.42");
        var second = provider.GetVisitorId("192.0.2.42");

        Assert.Equal(first, second);
    }

    [Fact]
    public void GetVisitorId_DifferentAddresses_ReturnDifferentIds()
    {
        var provider = CreateProvider();

        var first = provider.GetVisitorId("192.0.2.42");
        var second = provider.GetVisitorId("192.0.2.43");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GetVisitorId_EquivalentIpv6Addresses_ReturnSameId()
    {
        var provider = CreateProvider();

        var expanded = provider.GetVisitorId(
            "2001:0db8:0000:0000:0000:0000:0000:0001");

        var compressed = provider.GetVisitorId("2001:db8::1");

        Assert.Equal(expanded, compressed);
    }

    [Fact]
    public void GetVisitorId_Ipv4MappedIpv6_ReturnsSameIdAsIpv4()
    {
        var provider = CreateProvider();

        var ipv4 = provider.GetVisitorId("192.0.2.42");
        var mapped = provider.GetVisitorId("::ffff:192.0.2.42");

        Assert.Equal(ipv4, mapped);
    }

    [Fact]
    public void GetVisitorId_InvalidAddress_Throws()
    {
        var provider = CreateProvider();

        Assert.Throws<ArgumentException>(
            () => provider.GetVisitorId("definitely-not-an-ip"));
    }

    [Fact]
    public void GetVisitorId_DifferentKeys_ReturnDifferentIds()
    {
        var first = CreateProvider("key-number-one-that-is-long-enough");
        var second = CreateProvider("key-number-two-that-is-long-enough");

        var firstId = first.GetVisitorId("192.0.2.42");
        var secondId = second.GetVisitorId("192.0.2.42");

        Assert.NotEqual(firstId, secondId);
    }

    [Fact]
    public void GetVisitorId_MissingKey_ReturnsNull()
    {
        var provider = new VisitorIdProvider(
            Microsoft.Extensions.Options.Options.Create(
                new TelemetryOptions()));

        var visitorId = provider.GetVisitorId("192.0.2.42");

        Assert.Null(visitorId);
    }

    [Fact]
    public void GetVisitorId_DoesNotContainOriginalAddress()
    {
        var provider = CreateProvider();
        const string address = "192.0.2.42";

        var visitorId = provider.GetVisitorId(address);

        Assert.DoesNotContain(address, visitorId);
        Assert.Equal(64, visitorId.Length);
    }

    private static VisitorIdProvider CreateProvider(
        string key = TestKey)
    {
        return new VisitorIdProvider(
            Microsoft.Extensions.Options.Options.Create(
                new TelemetryOptions
                {
                    VisitorHashKey = key
                }));
    }
}