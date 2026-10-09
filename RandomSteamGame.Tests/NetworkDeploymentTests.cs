using AngleSharp.Html.Parser;
using Ganss.Xss;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OptionsFactory = Microsoft.Extensions.Options.Options;
using RandomSteamGame.Options;
using RandomSteamGame.Services;
using SteamApiClient.Contracts.SteamApi;
using SteamApiClient.Contracts.SteamStoreApi;
using SteamApiClient.HttpClients;
using SteamApiClient.Services;
using System.Net;
using System.Text.Json;

namespace RandomSteamGame.Tests;

public sealed class NetworkDeploymentTests
{
    [Theory]
    [InlineData("I2P")]
    [InlineData("Yggdrasil")]
    [InlineData("DN42")]
    [InlineData("Private community network")]
    public void Configuration_BindsAltNetWithArbitraryDisplayName(string networkName)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Application:NetworkMode"] = "AltNet",
                ["Application:NetworkName"] = networkName
            }).Build();
        var settings = configuration.GetSection(ApplicationOptions.SectionName).Get<ApplicationOptions>()!;

        Assert.Equal(NetworkMode.AltNet, settings.NetworkMode);
        Assert.Equal(networkName, settings.NetworkDisplayName);
        Assert.False(settings.RemoteBrowserAssetsAllowed);
        Assert.False(settings.BetaProbeEnabled);
    }

    [Theory]
    [InlineData(NetworkMode.Public, null, true)]
    [InlineData(NetworkMode.AltNet, null, false)]
    [InlineData(NetworkMode.Public, false, false)]
    [InlineData(NetworkMode.AltNet, true, true)]
    public async Task SteamGameResponse_AppliesAssetPolicyToHeaderAndDescription(
        NetworkMode mode, bool? allowRemoteAssets, bool expectRemoteAssets)
    {
        var settings = OptionsFactory.Create(new ApplicationOptions
        {
            NetworkMode = mode,
            AllowRemoteBrowserAssets = allowRemoteAssets
        });
        var provider = new SteamProvider(
            new StubSteamClient(), new StubSteamStoreClient(), new HttpContextAccessor(),
            new HtmlSanitizerService(settings), NullLogger<SteamProvider>.Instance, settings);

        // Both API calls and initial render state originate from this response mapping.
        var result = await provider.GetRandomGameDetailsAsync(76561197960287930L);

        Assert.False(result.IsError);
        Assert.Equal("Test Game", result.Value.Name);
        Assert.Equal(expectRemoteAssets ? HeaderImage : string.Empty, result.Value.HeaderImage);
        var description = new HtmlParser().ParseDocument(result.Value.Description);
        Assert.Equal(expectRemoteAssets, description.QuerySelector("img") is not null);
        Assert.Equal("Game text", description.QuerySelector("strong")?.TextContent);
        Assert.Equal("https://example.com/details", description.QuerySelector("a")?.GetAttribute("href"));
        Assert.Equal(mode == NetworkMode.Public, settings.Value.BetaProbeEnabled);
    }

    [Fact]
    public void PublicDescription_UsesUnchangedDefaultSanitizer()
    {
        const string html = """
            <h2>Title</h2><p style="color: red">Text <b>bold</b></p>
            <img src="https://cdn.example/image.png"><a href="https://example.com">More</a>
            <script>alert(1)</script><img src="javascript:alert(1)" onerror="alert(1)">
            """;
        var service = new HtmlSanitizerService(OptionsFactory.Create(new ApplicationOptions()));

        Assert.Equal(new HtmlSanitizer().Sanitize(html), service.Sanitize(html));
    }

    [Theory]
    [InlineData("<img src='https://cdn.example/image.png' srcset='https://cdn.example/large.png 2x'>")]
    [InlineData("<img src='//cdn.example/image.png'><img src='/local.png'>")]
    [InlineData("<p style='background-image: url(https://cdn.example/image.png)'>Text</p>")]
    [InlineData("<style>@import url(https://cdn.example/fonts.css);</style><link rel='stylesheet' href='https://cdn.example/style.css'>")]
    [InlineData("<iframe src='https://cdn.example/'></iframe><object data='https://cdn.example/file'><embed src='https://cdn.example/file'></object>")]
    [InlineData("<video poster='https://cdn.example/poster.png'><source src='https://cdn.example/movie.mp4'></video><audio src='https://cdn.example/audio.mp3'></audio>")]
    [InlineData("<form action='https://cdn.example/'><input type='image' src='https://cdn.example/button.png'></form>")]
    [InlineData("<svg><image href='https://cdn.example/image.png'></image></svg>")]
    [InlineData("<table background='https://cdn.example/image.png'><tr><td>Text</td></tr></table>")]
    public void RestrictedDescription_RemovesAutomaticallyLoadedResources(string html)
    {
        var service = new HtmlSanitizerService(OptionsFactory.Create(new ApplicationOptions
        {
            NetworkMode = NetworkMode.AltNet
        }));

        var sanitized = service.Sanitize(html);
        var document = new HtmlParser().ParseDocument(sanitized);

        Assert.Empty(document.QuerySelectorAll(
            "img, style, link, iframe, object, embed, video, audio, source, input, svg, form, " +
            "[src], [srcset], [style], [poster], [background], [data]"));
        Assert.DoesNotContain("cdn.example", sanitized);
    }

    [Fact]
    public void RestrictedDescription_PreservesFormattingAndLinksWithoutAutomaticLinkRequests()
    {
        const string html = """
            <h2>Title</h2><p>Ordinary <strong>bold</strong> and <em>italic</em> text.</p>
            <ul><li>One</li></ul><table><tr><td colspan="2">Cell</td></tr></table>
            <a href="https://example.com" ping="https://cdn.example/track" style="background:url(https://cdn.example/bg)">More</a>
            """;
        var service = new HtmlSanitizerService(OptionsFactory.Create(new ApplicationOptions
        {
            NetworkMode = NetworkMode.AltNet
        }));
        var document = new HtmlParser().ParseDocument(service.Sanitize(html));

        Assert.Equal("Title", document.QuerySelector("h2")?.TextContent);
        Assert.Equal("bold", document.QuerySelector("strong")?.TextContent);
        Assert.Equal("italic", document.QuerySelector("em")?.TextContent);
        Assert.Equal("One", document.QuerySelector("li")?.TextContent);
        Assert.Equal("2", document.QuerySelector("td")?.GetAttribute("colspan"));
        Assert.Equal("https://example.com", document.QuerySelector("a")?.GetAttribute("href"));
        Assert.Empty(document.QuerySelectorAll("[ping], [style]"));
    }

    [Theory]
    [InlineData(NetworkMode.Public, null, true)]
    [InlineData(NetworkMode.AltNet, null, false)]
    [InlineData(NetworkMode.Public, false, false)]
    [InlineData(NetworkMode.AltNet, true, true)]
    public async Task BetaProbe_RespectsModeAndOverrideBeforeCreatingHttpClient(
        NetworkMode mode, bool? enableBetaProbe, bool expectProbe)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var factory = new CountingHttpClientFactory();
        var settings = OptionsFactory.Create(new ApplicationOptions
        {
            NetworkMode = mode,
            EnableBetaProbe = enableBetaProbe
        });
        var service = new BetaAvailabilityService(
            cache, factory, NullLogger<BetaAvailabilityService>.Instance, settings);

        Assert.Equal(expectProbe, await service.IsBetaAvailableAsync(TestContext.Current.CancellationToken));
        Assert.Equal(expectProbe, await service.IsBetaAvailableAsync(TestContext.Current.CancellationToken));
        Assert.Equal(expectProbe ? 1 : 0, factory.CreateCount);
        Assert.Equal(expectProbe ? 1 : 0, factory.RequestCount);
        Assert.Equal(mode == NetworkMode.Public, settings.Value.RemoteBrowserAssetsAllowed);
    }

    private const string HeaderImage = "https://cdn.example/header.jpg";
    private const string Description = "<p><strong>Game text</strong><img src='https://cdn.example/description.jpg'><a href='https://example.com/details'>More</a></p>";

    private sealed class StubSteamClient : ISteamClient
    {
        private static readonly OwnedGames Library = new(1,
            [new Game(10, "Test Game", 0, null, 0, 0, 0, 0, 0)]);

        public Task<OwnedGames> GetOwnedGames(long steamId, bool includeAppInfo = true,
            bool includePlayedFreeGames = true, CancellationToken ct = default) => Task.FromResult(Library);

        public Task<OwnedGamesResult> GetOwnedGamesWithCacheInfo(long steamId, bool includeAppInfo = true,
            bool includePlayedFreeGames = true, CancellationToken ct = default) =>
            Task.FromResult(new OwnedGamesResult(Library, OwnedGamesCacheInfo.Unknown));

        public Task<long> GetSteamIdFromVanityUrl(string vanityUrl, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyDictionary<int, SteamDeckCompatibilityCategory>> GetSteamDeckCompatibilityAsync(
            IEnumerable<int> appIds, CancellationToken ct = default) => throw new NotSupportedException();

        public Task InvalidateOwnedGamesCacheAsync(long steamId) => throw new NotSupportedException();
    }

    private sealed class StubSteamStoreClient : ISteamStoreClient
    {
        private readonly AppData _app = JsonSerializer.Deserialize<AppData>(JsonSerializer.Serialize(new
        {
            steam_appid = 10,
            name = "Test Game",
            about_the_game = Description,
            header_image = HeaderImage
        }))!;

        public Task<AppData?> GetAppData(int appId, IEnumerable<string>? tags = null,
            CancellationToken ct = default) => Task.FromResult<AppData?>(_app);
    }

    private sealed class CountingHttpClientFactory : HttpMessageHandler, IHttpClientFactory
    {
        public int CreateCount { get; private set; }
        public int RequestCount { get; private set; }

        public HttpClient CreateClient(string name)
        {
            CreateCount++;
            return new HttpClient(this, disposeHandler: false);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            Assert.Equal("https://randombeta.kgivler.com/api/stats", request.RequestUri?.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
