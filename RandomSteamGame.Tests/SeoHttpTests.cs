using AngleSharp;
using AngleSharp.Dom;
using JoyfulReaperLib.Caching.Sqlite;
using JoyfulReaperLib.MissionControl;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RandomSteamGame.Client.Services;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Xml.Linq;

namespace RandomSteamGame.Tests;

public sealed class SeoHttpTests : IClassFixture<SeoWebApplicationFactory>
{
    private const string CanonicalOrigin = "https://randomsteam.kgivler.com";
    private const string HomeTitle = "Random Steam Game Picker – Pick From Your Library";

    private readonly HttpClient _client;

    public SeoHttpTests(SeoWebApplicationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Home_ReturnsServerRenderedSeoMetadata()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync("/", cancellationToken);
        var document = await ParseHtmlAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HomeTitle, document.Title);
        Assert.Equal(CanonicalOrigin, GetAttribute(document, "link[rel='canonical']", "href"));

        var description = GetAttribute(document, "meta[name='description']", "content");
        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.Equal(HomeTitle, GetAttribute(document, "meta[property='og:title']", "content"));
        Assert.Equal(description, GetAttribute(document, "meta[property='og:description']", "content"));
        Assert.Equal(CanonicalOrigin, GetAttribute(document, "meta[property='og:url']", "content"));
        Assert.Equal("website", GetAttribute(document, "meta[property='og:type']", "content"));
        Assert.Equal("summary", GetAttribute(document, "meta[name='twitter:card']", "content"));
        Assert.Equal(HomeTitle, GetAttribute(document, "meta[name='twitter:title']", "content"));
        Assert.Equal(description, GetAttribute(document, "meta[name='twitter:description']", "content"));
        Assert.Equal("Random Steam Game Picker", document.QuerySelector("h1")?.TextContent.Trim());

        var structuredDataElement = Assert.IsAssignableFrom<IElement>(
            document.QuerySelector("script[type='application/ld+json']"));
        using var structuredData = JsonDocument.Parse(structuredDataElement.TextContent);
        var root = structuredData.RootElement;

        Assert.Equal("https://schema.org", root.GetProperty("@context").GetString());
        Assert.Equal("WebApplication", root.GetProperty("@type").GetString());
        Assert.Equal("Random Steam Game Picker", root.GetProperty("name").GetString());
        Assert.Equal(CanonicalOrigin, root.GetProperty("url").GetString());
        Assert.Equal(description, root.GetProperty("description").GetString());
        Assert.Equal("UtilitiesApplication", root.GetProperty("applicationCategory").GetString());
        Assert.Equal("Any device with a web browser", root.GetProperty("operatingSystem").GetString());
        Assert.True(root.GetProperty("isAccessibleForFree").GetBoolean());

        var featureList = root
            .GetProperty("featureList")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains(
            "Steam library CSV export",
            featureList);

        Assert.Contains(
            "Steam Deck compatibility included in library exports",
            featureList);
    }

    [Fact]
    public async Task Home_HostileHostHeader_DoesNotChangeCanonicalUrl()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = "evil.example";

        using var response = await _client.SendAsync(request, cancellationToken);
        var document = await ParseHtmlAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(CanonicalOrigin, GetAttribute(document, "link[rel='canonical']", "href"));
        Assert.Equal(CanonicalOrigin, GetAttribute(document, "meta[property='og:url']", "content"));
    }

    [Fact]
    public async Task Sitemap_ContainsOnlyCanonicalPublicUrls()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync("/sitemap.xml", cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var sitemap = XDocument.Parse(content);
        XNamespace sitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var locations = sitemap
            .Descendants(sitemapNamespace + "loc")
            .Select(element => element.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var expectedLocations = new[]
        {
            $"{CanonicalOrigin}/",
            $"{CanonicalOrigin}/contributors",
            $"{CanonicalOrigin}/library-export",
            $"{CanonicalOrigin}/support"
        }.Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedLocations, locations);
        Assert.DoesNotContain("/random-game/", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            sitemap.Descendants(),
            element => element.Name.LocalName is "priority" or "changefreq" or "lastmod");
    }

    [Fact]
    public async Task Robots_AdvertisesProductionSitemapAndAllowsCrawling()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync("/robots.txt", cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var directives = content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("User-agent: *", directives);
        Assert.Contains("Allow: /", directives);
        Assert.Contains($"Sitemap: {CanonicalOrigin}/sitemap.xml", directives);
        Assert.DoesNotContain(
            directives,
            directive => directive.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/api/stats")]
    [InlineData("/health/live")]
    [InlineData("/error")]
    public async Task NonPageEndpoint_ReturnsNoindexNofollowHeader(string path)
    {
        using var response = await _client.GetAsync(
            path,
            TestContext.Current.CancellationToken);

        Assert.Equal("noindex, nofollow", GetRobotsHeader(response));
    }

    [Fact]
    public async Task JsonApiResponse_ReturnsNoindexNofollowHeader()
    {
        using var response = await _client.GetAsync(
            "/api/steam/1/library",
            TestContext.Current.CancellationToken);

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("noindex, nofollow", GetRobotsHeader(response));
    }

    [Fact]
    public async Task DirectNotFoundPage_ReturnsNotFoundAndNoindexNofollow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync("/not-found", cancellationToken);
        var document = await ParseHtmlAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("noindex, nofollow", GetRobotsHeader(response));
        Assert.Equal(
            "noindex,nofollow",
            GetAttribute(document, "meta[name='robots']", "content").Replace(" ", string.Empty));
    }

    [Fact]
    public async Task DirectErrorPage_ReturnsInternalServerErrorAndNoindexNofollow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync("/Error", cancellationToken);
        var document = await ParseHtmlAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("noindex, nofollow", GetRobotsHeader(response));
        Assert.Equal(
            "noindex,nofollow",
            GetAttribute(document, "meta[name='robots']", "content").Replace(" ", string.Empty));
    }

    [Fact]
    public async Task UnknownRoute_ReturnsNotFoundAndNoindexNofollow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync(
            "/definitely-not-a-public-route",
            cancellationToken);
        var document = await ParseHtmlAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("noindex, nofollow", GetRobotsHeader(response));
        Assert.Equal(
            "noindex,nofollow",
            GetAttribute(document, "meta[name='robots']", "content").Replace(" ", string.Empty));
    }

    [Fact]
    public async Task SupportedRandomGame_ReturnsNoindexMetadataAndProductionCanonical()
    {
        const string path = "/random-game/steam/76561197960287930";
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await _client.GetAsync(path, cancellationToken);
        var document = await ParseHtmlAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("noindex, follow", GetRobotsHeader(response));
        Assert.Equal(
            "noindex,follow",
            GetAttribute(document, "meta[name='robots']", "content").Replace(" ", string.Empty));
        Assert.Equal(CanonicalOrigin + path, GetAttribute(document, "link[rel='canonical']", "href"));
    }

    [Fact]
    public async Task UnsupportedRandomGame_ReturnsNotFoundAndNoindexNofollowHeader()
    {
        using var response = await _client.GetAsync(
            "/random-game/gog/76561197960287930",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("noindex, nofollow", GetRobotsHeader(response));
    }

    [Fact]
    public async Task InvalidRandomGameRoute_ReturnsNotFoundAndNoindexNofollowHeader()
    {
        using var response = await _client.GetAsync(
            "/random-game/steam/not-a-number",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("noindex, nofollow", GetRobotsHeader(response));
    }

    [Fact]
    public async Task BetaHost_ReturnsNoindexNofollowHeader()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/support");
        request.Headers.Host = "randombeta.kgivler.com";

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("noindex, nofollow", GetRobotsHeader(response));
    }

    [Fact]
    public async Task LibraryExport_ReturnsSeoMetadata()
    {
        var cancellationToken =
            TestContext.Current.CancellationToken;

        using var response =
            await _client.GetAsync(
                "/library-export",
                cancellationToken);

        var document =
            await ParseHtmlAsync(
                response,
                cancellationToken);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        Assert.Equal(
            "Export Steam Library to CSV – Random Steam Game",
            document.Title);

        Assert.Equal(
            $"{CanonicalOrigin}/library-export",
            GetAttribute(
                document,
                "link[rel='canonical']",
                "href"));

        Assert.False(
            string.IsNullOrWhiteSpace(
                GetAttribute(
                    document,
                    "meta[name='description']",
                    "content")));
    }

    private static async Task<IDocument> ParseHtmlAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return await BrowsingContext.New(Configuration.Default).OpenAsync(
            request => request.Content(content),
            cancellationToken);
    }

    private static string GetAttribute(IDocument document, string selector, string attributeName)
    {
        var element = Assert.IsAssignableFrom<IElement>(document.QuerySelector(selector));
        return Assert.IsType<string>(element.GetAttribute(attributeName));
    }

    private static string GetRobotsHeader(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("X-Robots-Tag", out var values));
        return Assert.Single(values);
    }
}

public sealed class SeoWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dataProtectionPath = Path.Combine(
        Path.GetTempPath(),
        $"random-steam-game-seo-tests-{Guid.NewGuid():N}");
    private readonly string _databasePath = Path.Combine(AppContext.BaseDirectory, "Data", "kgivler_com.db");
    private readonly bool _databaseExisted;

    public SeoWebApplicationFactory()
    {
        _databaseExisted = File.Exists(_databasePath);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:CanonicalOrigin"] = "https://randomsteam.kgivler.com",
                ["Application:BetaHost"] = "randombeta.kgivler.com",
                ["DataProtection:KeysPath"] = _dataProtectionPath,
                ["MissionControl:Enabled"] = "false",
                ["Steam:ApiKey"] = "00000000000000000000000000000000",
                ["Steam:ConnectionString"] =
                    $"Data Source={Path.Combine(_dataProtectionPath, "steam-cache.db")};Pooling=False"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<SqliteDistributedCacheOptions>(options =>
            {
                options.ConnectionString = "Data Source=steam-cache.db;Pooling=False";
                options.BasePath = _dataProtectionPath;
            });

            services.RemoveAll<IGameProvider>();
            services.AddScoped<IGameProvider, SteamProvider>();

            services.RemoveAll<IAppStatsService>();
            services.AddScoped<IAppStatsService, StubAppStatsService>();

            services.RemoveAll<IBetaAvailabilityService>();
            services.AddSingleton<IBetaAvailabilityService, StubBetaAvailabilityService>();

            services.RemoveAll<IMissionControlClient>();
            services.AddSingleton<IMissionControlClient, StubMissionControlClient>();

            services.AddHttpClient<RandomSteamApiClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new StubHttpMessageHandler());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        SqliteConnection.ClearAllPools();
        DeleteDatabaseIfCreatedByTests();

        if (Directory.Exists(_dataProtectionPath))
        {
            Directory.Delete(_dataProtectionPath, recursive: true);
        }
    }

    private void DeleteDatabaseIfCreatedByTests()
    {
        if (_databaseExisted)
        {
            return;
        }

        DeleteIfExists(_databasePath);
        DeleteIfExists(_databasePath + "-wal");
        DeleteIfExists(_databasePath + "-shm");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private sealed class StubAppStatsService : IAppStatsService
    {
        private static readonly AppStatsResponse EmptyStats = new(0, 0, 0);

        public Task<AppStatsResponse> RecordHitAsync(string ip) => Task.FromResult(EmptyStats);

        public Task<AppStatsResponse> GetStatsAsync() => Task.FromResult(EmptyStats);

        public Task IncrementRandomGamesGeneratedAsync() => Task.CompletedTask;
    }

    private sealed class StubBetaAvailabilityService : IBetaAvailabilityService
    {
        public Task<bool> IsBetaAvailableAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class StubMissionControlClient : IMissionControlClient
    {
        public Task<bool> TryPublishAsync<TPayload>(
            string eventType,
            TPayload payload,
            JsonTypeInfo<TPayload> payloadTypeInfo,
            DateTimeOffset occurredAt,
            string? correlationId = null,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = JsonContent.Create(new
                {
                    title = "Unavailable during SEO integration tests",
                    status = StatusCodes.Status503ServiceUnavailable
                })
            });
    }
}
