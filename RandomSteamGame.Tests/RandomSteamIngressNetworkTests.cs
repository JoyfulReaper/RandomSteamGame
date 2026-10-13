using JoyfulReaperLib.JRNet;
using RandomSteamGame.Services;

namespace RandomSteamGame.Tests;

public class RandomSteamIngressNetworkTests
{
    [Theory]
    [InlineData("randomsteam.kgivler.com", "public")]
    [InlineData("RANDOMSTEAM.KGIVLER.COM", "public")]
    [InlineData(" randomsteam.kgivler.com ", "public")]
    [InlineData("randomsteam.kgivler.com.", "public")]
    [InlineData("other.kgivler.com", "unknown")]
    [InlineData("randomsteam.dn42", "dn42")]
    [InlineData("RANDOMSTEAM.DN42.", "dn42")]
    [InlineData("randomsteam.i2p", "i2p")]
    [InlineData("steam.ygg.kgivler.com", "yggdrasil")]
    [InlineData("STEAM.YGG.KGIVLER.COM.", "yggdrasil")]
    [InlineData("localhost", "unknown")]
    [InlineData("", "unknown")]
    [InlineData(null, "unknown")]
    public void FromHost_ClassifiesUsingRandomSteamRules(
        string? host,
        string expected)
    {
        var result = RandomSteamIngressNetwork.FromHost(host);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void FromHost_ReturnsSharedTelemetryValues()
    {
        Assert.Equal("public", IngressNetworkClassifier.Public);
        Assert.Equal("dn42", IngressNetworkClassifier.Dn42);
        Assert.Equal("i2p", IngressNetworkClassifier.I2p);
        Assert.Equal("yggdrasil", IngressNetworkClassifier.Yggdrasil);
        Assert.Equal("unknown", IngressNetworkClassifier.Unknown);
    }
}
