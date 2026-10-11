using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net;

namespace RandomSteamGame.Tests;

[Collection(nameof(ServerBrowserExecutionTests))]
public sealed class CorsHttpTests(SeoWebApplicationFactory factory) : IClassFixture<SeoWebApplicationFactory>
{
    [Fact]
    public void ProductionPolicy_PreservesExistingOriginsAndAddsOnlyTheI2pFrontend()
    {
        using var application = CreateProductionApplication();
        var policy = application.Services.GetRequiredService<IOptions<CorsOptions>>()
            .Value.GetPolicy("DefaultCors")!;

        Assert.Equal(new[]
        {
            "https://randomsteam.kgivler.com",
            "https://randombeta.kgivler.com",
            "https://kgivler.com",
            "https://www.kgivler.com",
            "http://kgivler.i2p",
            "http://site.ygg.kgivler.com",
            "https://kgivler.dn42",
            "http://kgivler.dn42",
            "http://[301:762f:80bd:20e1::40]"
        }, policy.Origins);
        Assert.False(policy.AllowAnyOrigin);
        Assert.False(policy.SupportsCredentials);
        Assert.Equal(new[] { "GET", "POST" }, policy.Methods);
    }

    [Theory]
    [InlineData("http://kgivler.i2p", true)]
    [InlineData("http://arbitrary.i2p", false)]
    [InlineData("http://subdomain.kgivler.i2p", false)]
    [InlineData("http://randomsteam.i2p", false)]
    [InlineData("https://kgivler.i2p", false)]
    [InlineData("http://localhost:5500", false)]
    [InlineData("https://randomsteam.kgivler.com", true)]
    [InlineData("https://randombeta.kgivler.com", true)]
    [InlineData("https://kgivler.com", true)]
    [InlineData("https://www.kgivler.com", true)]
    [InlineData("http://site.ygg.kgivler.com", true)]
    [InlineData("https://kgivler.dn42", true)]
    [InlineData("http://kgivler.dn42", true)]
    [InlineData("http://[301:762f:80bd:20e1::40]", true)]
    public async Task ProductionApiPreflight_AllowsOnlyConfiguredOrigins(string origin, bool allowed)
    {
        using var application = CreateProductionApplication();
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://randomsteam.i2p"),
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/steam/random-game/details");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        if (allowed)
        {
            Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        }
        else
        {
            Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        }
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    private WebApplicationFactory<Program> CreateProductionApplication() => factory.WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment(Environments.Production);
        // Model the I2P API deployment without overriding its production CORS list.
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Application:NetworkMode"] = "AltNet",
                ["Application:CanonicalOrigin"] = "http://randomsteam.i2p"
            }));
    });
}
