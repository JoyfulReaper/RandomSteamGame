using RandomSteamGame.Services;

namespace RandomSteamGame.Tests;

public class IngressNetworkClassifierTests
{
    [Theory]
    [InlineData("randomsteam.kgivler.com", "public")]
    [InlineData("RANDOMSTEAM.KGIVLER.COM", "public")]
    [InlineData(" randomsteam.kgivler.com ", "public")]
    [InlineData("randomsteam.kgivler.com.", "public")]

    [InlineData("randomsteam.dn42", "dn42")]
    [InlineData("kgivler.dn42", "dn42")]
    [InlineData("something.deep.dn42", "dn42")]
    [InlineData("RANDOMSTEAM.DN42", "dn42")]
    [InlineData("randomsteam.dn42.", "dn42")]

    [InlineData("steam.ygg.kgivler.com", "yggdrasil")]
    [InlineData("whatever.ygg.kgivler.com", "yggdrasil")]
    [InlineData("deep.name.ygg.kgivler.com", "yggdrasil")]
    [InlineData("STEAM.YGG.KGIVLER.COM", "yggdrasil")]
    [InlineData("steam.ygg.kgivler.com.", "yggdrasil")]

    [InlineData("localhost", "unknown")]
    [InlineData("kgivler.com", "unknown")]
    [InlineData("randomsteam.example.com", "unknown")]
    [InlineData("notdn42.example", "unknown")]
    [InlineData("", "unknown")]
    [InlineData("   ", "unknown")]
    [InlineData(null, "unknown")]
    public void FromHost_ClassifiesExpectedNetwork(
        string? host,
        string expected)
    {
        var result =
            IngressNetworkClassifier.FromHost(host);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FromHost_ReturnsDefinedConstantValues()
    {
        Assert.Equal(
            "public",
            IngressNetworkClassifier.Public);

        Assert.Equal(
            "dn42",
            IngressNetworkClassifier.Dn42);

        Assert.Equal(
            "yggdrasil",
            IngressNetworkClassifier.Yggdrasil);

        Assert.Equal(
            "unknown",
            IngressNetworkClassifier.Unknown);
    }
}