using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RandomSteamGame.Services.Interfaces;
using System.Net;

namespace RandomSteamGame.Tests;

[Collection(nameof(ServerBrowserExecutionTests))]
public sealed class SupportPageTests(SeoWebApplicationFactory factory) : IClassFixture<SeoWebApplicationFactory>
{
    [Theory]
    [InlineData("Public", "https://randomsteam.kgivler.com")]
    [InlineData("AltNet", "http://example.b32.i2p")]
    public async Task SupportRendersUsefulLinksAndClearSponsorshipWithoutAccessingVoting(string mode, string origin)
    {
        using var app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:NetworkMode"] = mode, ["Application:CanonicalOrigin"] = origin
            }));
            builder.ConfigureTestServices(services => services.AddScoped<IFeatureVotingService>(_ =>
                throw new InvalidOperationException("Support prerender must not access voting persistence.")));
        });
        using var client = app.CreateClient();
        using var response = await client.GetAsync("/support", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var document = new HtmlParser().ParseDocument(html);
        Assert.Contains("free hobby project", document.Body!.TextContent);
        Assert.Contains("donations are appreciated and optional", document.Body.TextContent);
        Assert.Contains("Please contact me before sending payment for feature sponsorship.", document.Body.TextContent);
        Assert.Contains("do not guarantee development of a feature", document.Body.TextContent);
        Assert.Contains("United States", document.Body.TextContent);
        var urls = document.QuerySelectorAll("a").Select(a => a.GetAttribute("href")).ToArray();
        Assert.Contains("https://ko-fi.com/joyfulreaper", urls);
        Assert.Contains("https://www.paypal.com/paypalme/KyleGivler/5USD", urls);
        Assert.Contains("https://dev.kgivler.com/#contact", urls);
        Assert.Contains("https://dev.kgivler.com/", urls);
        Assert.Contains("https://github.com/JoyfulReaper/RandomSteamGame/issues/new", urls);
        Assert.Empty(document.QuerySelectorAll(".boost-card,.roadmap-table,.support-tier"));
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.Contains("FeatureVoter")));
        if (mode == "AltNet")
        {
            Assert.Contains("Feature voting is available on the clearnet site", document.Body.TextContent);
            Assert.Null(document.QuerySelector(".feature-board"));
        }
        else Assert.Contains("Loading features", document.Body.TextContent);
    }

    [Fact]
    public async Task PickerSupportLinksRemainInTheInitialHtml()
    {
        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/", TestContext.Current.CancellationToken);
        var document = new HtmlParser().ParseDocument(html);
        Assert.NotNull(document.QuerySelector(".home-card a[href='/support']"));
        Assert.NotNull(document.QuerySelector(".home-card a[href='https://ko-fi.com/joyfulreaper']"));
    }
}
