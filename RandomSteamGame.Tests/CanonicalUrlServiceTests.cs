using Microsoft.Extensions.Options;
using RandomSteamGame.Options;
using RandomSteamGame.Services;

namespace RandomSteamGame.Tests;

public sealed class CanonicalUrlServiceTests
{
    [Fact]
    public void GetCanonicalUrl_PublicDefaultIsUnchanged()
    {
        var service = new CanonicalUrlService(Microsoft.Extensions.Options.Options.Create(new ApplicationOptions()));

        Assert.Equal("https://randomsteam.kgivler.com", service.GetCanonicalUrl());
        Assert.Equal("https://randomsteam.kgivler.com/support", service.GetCanonicalUrl("/support"));
    }

    [Theory]
    [InlineData("http://example.b32.i2p", "/", "http://example.b32.i2p")]
    [InlineData("http://example.b32.i2p/", "/support", "http://example.b32.i2p/support")]
    [InlineData("https://randomsteam.dn42", "/contributors", "https://randomsteam.dn42/contributors")]
    [InlineData("http://some-ygg-host:8080", "/library-export", "http://some-ygg-host:8080/library-export")]
    [InlineData("http://[301:762f:80bd:20e1::40]", "/support", "http://[301:762f:80bd:20e1::40]/support")]
    public void GetCanonicalUrl_AltNetUsesExplicitHttpOrHttpsOrigin(string origin, string path, string expected)
    {
        var service = new CanonicalUrlService(Microsoft.Extensions.Options.Options.Create(new ApplicationOptions
        {
            NetworkMode = NetworkMode.AltNet,
            NetworkName = "Arbitrary display name",
            CanonicalOrigin = origin
        }));

        Assert.Equal(expected, service.GetCanonicalUrl(path));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("example.b32.i2p")]
    [InlineData("//example.b32.i2p")]
    [InlineData("ftp://example.b32.i2p")]
    [InlineData("http://user:password@example.b32.i2p")]
    [InlineData("http://example.b32.i2p/path")]
    [InlineData("http://example.b32.i2p?query=value")]
    [InlineData("http://example.b32.i2p#fragment")]
    public void Constructor_AltNetRejectsMissingOrInvalidOrigin(string? origin)
    {
        var settings = Microsoft.Extensions.Options.Options.Create(new ApplicationOptions
        {
            NetworkMode = NetworkMode.AltNet,
            CanonicalOrigin = origin
        });

        Assert.Throws<InvalidOperationException>(() => new CanonicalUrlService(settings));
    }

    [Theory]
    [InlineData("/", "https://randomsteam.kgivler.com")]
    [InlineData("/support", "https://randomsteam.kgivler.com/support")]
    [InlineData("support", "https://randomsteam.kgivler.com/support")]
    [InlineData("/contributors", "https://randomsteam.kgivler.com/contributors")]
    [InlineData("contributors", "https://randomsteam.kgivler.com/contributors")]
    public void GetCanonicalUrl_UsesConfiguredOrigin(string path, string expected)
    {
        var service = CreateService();

        var result = service.GetCanonicalUrl(path);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("https://evil.example/test")]
    [InlineData("http://evil.example/test")]
    public void GetCanonicalUrl_RejectsAbsoluteUrl(string path)
    {
        var service = CreateService();

        Assert.Throws<ArgumentException>(() => service.GetCanonicalUrl(path));
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
