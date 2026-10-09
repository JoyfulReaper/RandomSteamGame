using AngleSharp;
using AngleSharp.Dom;
using ErrorOr;
using JoyfulReaperLib.Caching.Sqlite;
using JoyfulReaperLib.MissionControl;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using RandomSteamGame.Client.Services;
using RandomSteamGame.Common.Errors;
using RandomSteamGame.Options;
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
    private const string AltNetOrigin = "http://example.b32.i2p";
    private const string HomeTitle = "Random Steam Game Picker – Pick From Your Library";

    public static TheoryData<string, string, string, string> IndexablePageCases => new()
    {
        { "/", HomeTitle, CanonicalOrigin, "Random Steam Game Picker" },
        {
            "/library-export",
            "Export Steam Library to CSV – Random Steam Game",
            $"{CanonicalOrigin}/library-export",
            "Export Your Steam Library"
        },
        {
            "/support",
            "Support Random Steam Game Picker",
            $"{CanonicalOrigin}/support",
            "Support the Picker"
        },
        {
            "/contributors",
            "Contributors - Random Steam Game Picker",
            $"{CanonicalOrigin}/contributors",
            "Contributors & Sponsors"
        }
    };

    private readonly SeoWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SeoHttpTests(SeoWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Theory]
    [MemberData(nameof(IndexablePageCases))]
    public async Task IndexablePage_ReturnsCompleteServerRenderedMetadata(
        string path,
        string expectedTitle,
        string expectedCanonicalUrl,
        string expectedH1)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync(path, cancellationToken);
        var document = await ParseHtmlAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedTitle, document.Title);

        var description = GetAttribute(document, "meta[name='description']", "content");
        Assert.False(string.IsNullOrWhiteSpace(description));
        Assert.Equal(expectedCanonicalUrl, GetAttribute(document, "link[rel='canonical']", "href"));
        Assert.Equal(expectedTitle, GetAttribute(document, "meta[property='og:title']", "content"));
        Assert.Equal(description, GetAttribute(document, "meta[property='og:description']", "content"));
        Assert.Equal(expectedCanonicalUrl, GetAttribute(document, "meta[property='og:url']", "content"));
        Assert.Equal("website", GetAttribute(document, "meta[property='og:type']", "content"));
        Assert.Equal("summary", GetAttribute(document, "meta[name='twitter:card']", "content"));
        Assert.Equal(expectedTitle, GetAttribute(document, "meta[name='twitter:title']", "content"));
        Assert.Equal(description, GetAttribute(document, "meta[name='twitter:description']", "content"));
        Assert.Equal(expectedH1, document.QuerySelector("h1")?.TextContent.Trim());
    }

    [Theory]
    [InlineData(NetworkMode.Public, null, true)]
    [InlineData(NetworkMode.AltNet, null, false)]
    [InlineData(NetworkMode.Public, false, false)]
    [InlineData(NetworkMode.AltNet, true, true)]
    public async Task Home_NetworkSettingsControlBetaProbeBannerAndArtworkNote(
        NetworkMode mode, bool? enableBetaProbe, bool expectBeta)
    {
        var betaService = new AvailableBetaService();
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.Configure<ApplicationOptions>(options =>
                {
                    options.NetworkMode = mode;
                    options.NetworkName = "Community mesh";
                    options.EnableBetaProbe = enableBetaProbe;
                    options.CanonicalOrigin = mode == NetworkMode.AltNet ? AltNetOrigin : CanonicalOrigin;
                });
                services.RemoveAll<IBetaAvailabilityService>();
                services.AddSingleton<IBetaAvailabilityService>(betaService);
            });
        });
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync("/", cancellationToken);
        var document = await ParseHtmlAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectBeta ? 1 : 0, betaService.CallCount);
        Assert.Equal(expectBeta && mode == NetworkMode.Public, document.QuerySelector(".home-beta-banner") is not null);
        if (mode == NetworkMode.AltNet)
        {
            Assert.Contains("Community mesh deployment", document.Body?.TextContent);
            Assert.DoesNotContain("Game background images are loaded from clearnet", document.Body?.TextContent);
            Assert.DoesNotContain("randomsteam.kgivler.com", document.DocumentElement.OuterHtml);
            Assert.DoesNotContain("randombeta.kgivler.com", document.DocumentElement.OuterHtml);
        }
        else
        {
            Assert.Contains("Game background images are loaded from clearnet", document.Body?.TextContent);
        }

        var expectedOrigin = mode == NetworkMode.AltNet ? AltNetOrigin : CanonicalOrigin;
        Assert.Equal(expectedOrigin, GetAttribute(document, "link[rel='canonical']", "href"));
        Assert.Equal(expectedOrigin, GetAttribute(document, "meta[property='og:url']", "content"));
    }

    [Theory]
    [MemberData(nameof(IndexablePageCases))]
    public async Task AltNet_IndexablePagesUseConfiguredOriginAndDoNotExposePublicHost(
        string path, string expectedTitle, string publicCanonicalUrl, string expectedH1)
    {
        using var factory = CreateAltNetFactory();
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = "evil.example";
        using var response = await client.SendAsync(request, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = await ParseHtmlAsync(response, cancellationToken);
        var expectedCanonicalUrl = publicCanonicalUrl.Replace(CanonicalOrigin, AltNetOrigin, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedTitle, document.Title);
        Assert.Equal(expectedH1, document.QuerySelector("h1")?.TextContent.Trim());
        Assert.Equal(expectedCanonicalUrl, GetAttribute(document, "link[rel='canonical']", "href"));
        Assert.Equal(expectedCanonicalUrl, GetAttribute(document, "meta[property='og:url']", "content"));
        Assert.DoesNotContain("randomsteam.kgivler.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("randombeta.kgivler.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Community mesh", document.QuerySelector($"a[href='{AltNetOrigin}/']")?.TextContent);
        Assert.NotNull(document.QuerySelector("a[href='https://github.com/JoyfulReaper/RandomSteamGame']"));

        if (path == "/")
        {
            var script = Assert.IsAssignableFrom<IElement>(document.QuerySelector("script[type='application/ld+json']"));
            using var structuredData = JsonDocument.Parse(script.TextContent);
            Assert.Equal(AltNetOrigin, structuredData.RootElement.GetProperty("url").GetString());
            Assert.Equal("https://schema.org", structuredData.RootElement.GetProperty("@context").GetString());
        }
    }

    [Fact]
    public async Task AltNet_RandomGameUsesConfiguredCanonicalOrigin()
    {
        using var factory = CreateAltNetFactory();
        using var client = factory.CreateClient();
        const string path = "/random-game/steam/76561197960287930";
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync(path, cancellationToken);
        var document = await ParseHtmlAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AltNetOrigin + path, GetAttribute(document, "link[rel='canonical']", "href"));
        Assert.Equal("noindex, follow", GetRobotsHeader(response));
        Assert.DoesNotContain("randomsteam.kgivler.com", document.DocumentElement.OuterHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/not-found", HttpStatusCode.NotFound)]
    [InlineData("/definitely-not-a-public-route", HttpStatusCode.NotFound)]
    [InlineData("/Error", HttpStatusCode.InternalServerError)]
    public async Task AltNet_ErrorPagesDoNotExposePublicHost(string path, HttpStatusCode expectedStatus)
    {
        using var factory = CreateAltNetFactory();
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync(path, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("noindex, nofollow", GetRobotsHeader(response));
        Assert.DoesNotContain("randomsteam.kgivler.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("randombeta.kgivler.com", html, StringComparison.OrdinalIgnoreCase);
        if (expectedStatus == HttpStatusCode.NotFound)
        {
            Assert.Contains($"curl -I {AltNetOrigin}/requested_route", html);
        }
    }

    [Fact]
    public async Task AltNet_BetaHostDoesNotRenderPublicBetaNoticeEvenWhenProbeEnabled()
    {
        using var altNetFactory = CreateAltNetFactory();
        using var factory = altNetFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<ApplicationOptions>(options => options.EnableBetaProbe = true)));
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = "randombeta.kgivler.com";
        using var response = await client.SendAsync(request, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = await ParseHtmlAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(document.QuerySelector(".home-beta-note"));
        Assert.Equal(AltNetOrigin, GetAttribute(document, "link[rel='canonical']", "href"));
        Assert.DoesNotContain("randomsteam.kgivler.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("randombeta.kgivler.com", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AltNet_RobotsDisallowsCrawlingWithoutAdvertisingSitemap()
    {
        using var factory = CreateAltNetFactory();
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await client.GetAsync("/robots.txt", cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("User-agent: *\nDisallow: /\n", content);
        Assert.DoesNotContain("Sitemap:", content);
        Assert.DoesNotContain("randomsteam.kgivler.com", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AltNet_SitemapUsesOnlyConfiguredOrigin()
    {
        using var factory = CreateAltNetFactory();
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/sitemap.xml");
        request.Headers.Host = "evil.example";
        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var sitemap = XDocument.Parse(content);
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new[] { AltNetOrigin + "/", AltNetOrigin + "/support", AltNetOrigin + "/contributors", AltNetOrigin + "/library-export" },
            sitemap.Descendants(ns + "loc").Select(element => element.Value));
        Assert.DoesNotContain("randomsteam.kgivler.com", content, StringComparison.OrdinalIgnoreCase);
    }

    private WebApplicationFactory<Program> CreateAltNetFactory() =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:NetworkMode"] = "AltNet",
                ["Application:NetworkName"] = "Community mesh",
                ["Application:CanonicalOrigin"] = AltNetOrigin
            })));

    [Fact]
    public void AltNet_MissingOriginFailsOptionsValidation()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Application:NetworkMode"] = "AltNet",
                    ["Application:CanonicalOrigin"] = null
                })));

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("Application:CanonicalOrigin", exception.Message);
        Assert.Contains("AltNet requires an explicit HTTP or HTTPS origin", exception.Message);
    }

    private sealed class AvailableBetaService : IBetaAvailabilityService
    {
        public int CallCount { get; private set; }

        public Task<bool> IsBetaAvailableAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task Home_ReturnsValidStructuredData()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync("/", cancellationToken);
        var document = await ParseHtmlAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var description = GetAttribute(document, "meta[name='description']", "content");

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
    public async Task Home_ReturnsSeoCriticalContentInInitialHtml()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync("/", cancellationToken);
        var document = await ParseHtmlAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Random Steam Game Picker", document.QuerySelector("h1")?.TextContent.Trim());

        Assert.Contains("Libraries Exported:", document.Body?.TextContent, StringComparison.Ordinal);

        // The picker prerenders a loading shell instead of usable form controls.
        // This prevents input from being entered before Blazor becomes interactive.
        Assert.Null(document.QuerySelector("#steamIdInput"));
        Assert.NotNull(document.QuerySelector(".picker-form-loading"));
        Assert.Contains("How it works", document.Body?.TextContent, StringComparison.Ordinal);
        Assert.Contains("Frequently asked questions", document.Body?.TextContent, StringComparison.Ordinal);
        Assert.NotNull(document.QuerySelector("a[href='/library-export']"));
    }

    [Fact]
    public async Task LibraryExport_ReturnsSeoCriticalContentInInitialHtml()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var response = await _client.GetAsync("/library-export", cancellationToken);
        var document = await ParseHtmlAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Export Your Steam Library", document.QuerySelector("h1")?.TextContent.Trim());
        Assert.Contains("Download your public Steam library as CSV", document.Body?.TextContent, StringComparison.Ordinal);
        Assert.Contains("What is included?", document.Body?.TextContent, StringComparison.Ordinal);

        var exportForm = Assert.IsAssignableFrom<IElement>(document.QuerySelector("form"));
        Assert.Contains("Download Steam Library CSV", exportForm.TextContent, StringComparison.Ordinal);
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

    [Theory]
    [InlineData("/")]
    [InlineData("/random-game/steam/76561197960287930")]
    [InlineData("/health/live")]
    [InlineData("/support")]
    public async Task BetaHost_ReturnsNoindexNofollowHeader(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = "randombeta.kgivler.com";

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("noindex, nofollow", GetRobotsHeader(response));
    }

    [Fact]
    public async Task ProductionExceptionHandler_ReturnsServerRenderedNoindexErrorPage()
    {
        using var productionFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAppStatsService>();
                services.AddScoped<IAppStatsService, ThrowingAppStatsService>();
            });
        });
        using var client = productionFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await client.GetAsync("/", cancellationToken);
        var document = await ParseHtmlAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("noindex, nofollow", GetRobotsHeader(response));
        Assert.Equal("500: CORE UNSTABLE", document.Title);
        Assert.Equal(
            "noindex,nofollow",
            GetAttribute(document, "meta[name='robots']", "content").Replace(" ", string.Empty));
        Assert.False(string.IsNullOrWhiteSpace(
            GetAttribute(document, "meta[name='description']", "content")));
    }

    private sealed class ThrowingAppStatsService : IAppStatsService
    {
        public Task<AppStatsResponse> RecordHitAsync(
            string ip,
            string? userAgent = null,
            string ingressNetwork = IngressNetworkClassifier.Unknown) =>
            throw new InvalidOperationException(
                "Intentional production pipeline SEO test failure.");

        public Task<AppStatsResponse> GetStatsAsync() =>
            throw new InvalidOperationException(
                "Intentional production pipeline SEO test failure.");

        public Task IncrementRandomGamesGeneratedAsync() =>
            Task.CompletedTask;

        public Task IncrementLibrariesExportedAsync() =>
            Task.CompletedTask;
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
            // TestServer has no TCP peer. Model the existing trusted loopback ingress explicitly.
            services.AddSingleton<IStartupFilter, LoopbackPeerStartupFilter>();
            services.PostConfigure<SqliteDistributedCacheOptions>(options =>
            {
                options.ConnectionString = "Data Source=steam-cache.db;Pooling=False";
                options.BasePath = _dataProtectionPath;
            });

            services.RemoveAll<IGameProvider>();
            services.AddScoped<IGameProvider, UnavailableGameProvider>();

            services.RemoveAll<IAppStatsService>();
            services.AddScoped<IAppStatsService, StubAppStatsService>();

            services.RemoveAll<IBetaAvailabilityService>();
            services.AddSingleton<IBetaAvailabilityService, StubBetaAvailabilityService>();

            services.RemoveAll<IMissionControlClient>();
            services.AddSingleton<IMissionControlClient, StubMissionControlClient>();

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

        public Task<AppStatsResponse> RecordHitAsync(
            string ip,
            string? userAgent = null,
            string ingressNetwork = IngressNetworkClassifier.Unknown) =>
            Task.FromResult(EmptyStats);

        public Task<AppStatsResponse> GetStatsAsync() =>
            Task.FromResult(EmptyStats);

        public Task IncrementRandomGamesGeneratedAsync() =>
            Task.CompletedTask;

        public Task IncrementLibrariesExportedAsync() =>
            Task.CompletedTask;
    }

    private sealed class LoopbackPeerStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, continuation) =>
            {
                context.Connection.RemoteIpAddress ??= IPAddress.Loopback;
                return continuation();
            });
            next(app);
        };
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

    private sealed class UnavailableGameProvider : IGameProvider
    {
        public string ProviderKey => "steam";
        public Task<ErrorOr<OwnedGamesResponse>> GetOwnedGamesAsync(long userId, CancellationToken ct = default) =>
            Task.FromResult<ErrorOr<OwnedGamesResponse>>(Errors.Steam.SteamApiFailed);
        public Task<ErrorOr<GameDetails>> GetRandomGameDetailsAsync(long userId, bool unplayedOnly = false,
            CancellationToken ct = default) =>
            Task.FromResult<ErrorOr<GameDetails>>(Errors.Steam.SteamApiFailed);
        public Task<RandomGamePickAttempt> GetRandomGamePickAsync(long userId, bool unplayedOnly = false,
            IReadOnlyCollection<int>? excludedGameIds = null, CancellationToken ct = default) =>
            Task.FromResult(RandomGamePickAttempt.Failure([Errors.Steam.SteamApiFailed]));
        public Task<ErrorOr<long>> ResolveIdentifierAsync(string identifier, CancellationToken ct = default) =>
            Task.FromResult<ErrorOr<long>>(Errors.Steam.VanityResolutionFailed);
        public Task InvalidateOwnedGamesCacheAsync(long userId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
