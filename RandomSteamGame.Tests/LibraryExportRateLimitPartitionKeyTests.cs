using RandomSteamGame.Services;
using System.Net;

namespace RandomSteamGame.Tests;

public class LibraryExportRateLimitPartitionKeyTests
{
    [Theory]
    [InlineData("192.0.2.42", "192.0.2.42")]
    [InlineData("::ffff:192.0.2.42", "192.0.2.42")]
    [InlineData(
        "2001:db8:1234:5678:1111:2222:3333:4444",
        "2001:db8:1234:5678::/64")]
    [InlineData(
        "2001:db8:1234:5678:aaaa:bbbb:cccc:dddd",
        "2001:db8:1234:5678::/64")]
    [InlineData(
        "2001:db8:1234:5679::1",
        "2001:db8:1234:5679::/64")]
    public void From_NormalizesAddress(
        string address,
        string expected)
    {
        var result = LibraryExportRateLimitPartitionKey.From(
            IPAddress.Parse(address));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void From_NullAddress_ReturnsUnknown()
    {
        Assert.Equal(
            "unknown",
            LibraryExportRateLimitPartitionKey.From(null));
    }
}