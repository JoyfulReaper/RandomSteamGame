using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RandomSteamGame.Services;

namespace RandomSteamGame.Tests;

[Collection(nameof(ServerBrowserExecutionTests))]
public sealed class SteamStartupValidationTests(SeoWebApplicationFactory factory) : IClassFixture<SeoWebApplicationFactory>
{
    [Theory]
    [InlineData("Steam:Cache:OwnedGames:AbsoluteMinutes", "0")]
    [InlineData("Steam:RateLimiting:PermitLimit", "-1")]
    [InlineData("Steam:RateLimiting:WindowSeconds", "0")]
    [InlineData("Steam:ApiKey", "STEAM_API_KEY")]
    public void ApplicationStartupRejectsInvalidSteamConfiguration(string path, string value)
    {
        using var application = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { [path] = value })));
        var exception = Assert.Throws<OptionsValidationException>(() => application.CreateClient());
        Assert.Contains(path, exception.Message);
    }

    [Fact]
    public void LimiterCannotConsumeInvalidOptionsBeforeStartupValidation()
    {
        using var application = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Steam:RateLimiting:PermitLimit"] = "0" }));
            builder.ConfigureTestServices(services =>
            {
                // Resolve the singleton before Host.StartAsync/ValidateOnStart to exercise this direct consumer.
                using var provider = services.BuildServiceProvider();
                var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<SteamApiRequestLimiter>());
                Assert.Contains("Steam:RateLimiting:PermitLimit", exception.Message);
            });
        });
        Assert.Throws<OptionsValidationException>(() => application.CreateClient());
    }
}
