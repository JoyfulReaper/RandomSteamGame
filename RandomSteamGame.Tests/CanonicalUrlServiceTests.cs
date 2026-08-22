using Microsoft.Extensions.Options;
using RandomSteamGame.Options;
using RandomSteamGame.Services;

namespace RandomSteamGame.Tests;

public sealed class CanonicalUrlServiceTests
{
    [Theory]
    [InlineData("/", "https://randomsteam.kgivler.com")]
    [InlineData("/support", "https://randomsteam.kgivler.com/support")]
    [InlineData("contributors", "https://randomsteam.kgivler.com/contributors")]
    public void GetCanonicalUrl_UsesConfiguredOrigin(string path, string expected)
    {
        var service = CreateService();

        var result = service.GetCanonicalUrl(path);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("randombeta.kgivler.com")]
    [InlineData("RANDOMBETA.KGIVLER.COM")]
    [InlineData("randombeta.kgivler.com.")]
    public void IsBetaHost_MatchesConfiguredHost(string host)
    {
        var service = CreateService();

        Assert.True(service.IsBetaHost(host));
    }

    [Theory]
    [InlineData("http://randomsteam.kgivler.com")]
    [InlineData("https://randomsteam.kgivler.com/path")]
    [InlineData("https://randomsteam.kgivler.com?query=value")]
    public void Constructor_RejectsInvalidCanonicalOrigin(string canonicalOrigin)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new ApplicationOptions
        {
            CanonicalOrigin = canonicalOrigin
        });

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = new CanonicalUrlService(options);
        });
    }

    private static CanonicalUrlService CreateService()
    {
        return new CanonicalUrlService(Microsoft.Extensions.Options.Options.Create(new ApplicationOptions
        {
            CanonicalOrigin = "https://randomsteam.kgivler.com",
            BetaHost = "randombeta.kgivler.com"
        }));
    }
}
