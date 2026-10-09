using JoyfulReaperLib.MissionControl;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;
using RandomSteamGame.Client.Services;
using RandomSteamGame.Common.Errors;
using RandomSteamGame.Events;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using SteamApiClient.HttpClients;
using SteamApiClient.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Sdk = SteamApiClient.Contracts.SteamApi;
using Store = SteamApiClient.Contracts.SteamStoreApi;

namespace RandomSteamGame.Tests;

[Collection(nameof(ServerBrowserExecutionTests))]
public class ServerBrowserExecutionTests
{
    private const long SteamId = 76561197960287930L;

    [Theory]
    [InlineData("library")]
    [InlineData("vanity")]
    [InlineData("random-vanity")]
    [InlineData("refresh")]
    public async Task ServerMethods_ForwardCallerCancellationToSteam(string operation)
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var root = new SeoWebApplicationFactory();
        var calls = new List<string>();
        var steam = new StubSteamClient(10)
        {
            OnOperation = (name, ct) =>
            {
                Assert.Equal(caller.Token, ct);
                calls.Add(name);
                caller.Cancel();
                ct.ThrowIfCancellationRequested();
            }
        };
        var store = new StubStoreClient();
        using var application = CreateApplication(root, steam, store, new RejectOutgoingHttpHandler());
        using var scope = application.Services.CreateScope();
        var server = scope.ServiceProvider.GetRequiredService<IRandomSteamApiClient>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            switch (operation)
            {
                case "library": await server.GetOwnedGamesAsync("steam", SteamId, caller.Token); break;
                case "vanity": await server.ResolveVanityUrlAsync("steam", "some_vanity", caller.Token); break;
                case "random-vanity": await server.GetRandomGameDetailsAsync("steam", vanityUrl: "some_vanity", cancellationToken: caller.Token); break;
                case "refresh": await server.InvalidateOwnedGamesCacheAsync("steam", SteamId, caller.Token); break;
                default: throw new ArgumentOutOfRangeException(nameof(operation));
            }
        });

        Assert.Equal(operation == "random-vanity" ? "vanity" : operation, Assert.Single(calls));
        Assert.Empty(store.RequestedAppIds);
    }

    [Theory]
    [InlineData("http://attacker.invalid:8899", "Public")]
    [InlineData("https://unreachable-public.invalid", "Public")]
    [InlineData("http://unreachable.b32.i2p", "AltNet")]
    [InlineData("https://unreachable.dn42", "AltNet")]
    [InlineData("http://unreachable-ygg-host", "AltNet")]
    public async Task Prerender_UsesRequestExclusionsWithoutOutboundHttp(string origin, string mode)
    {
        using var root = new SeoWebApplicationFactory();
        var steam = new StubSteamClient(10, 20);
        var store = new StubStoreClient();
        var outgoing = new RejectOutgoingHttpHandler();
        var telemetry = new RecordingTelemetry();
        using var application = CreateApplication(root, steam, store, outgoing, telemetry, origin, mode);
        using var client = application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(origin), AllowAutoRedirect = false, HandleCookies = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/random-game/steam/{SteamId}");
        request.Headers.Add("Cookie", "ExcludedGameIds=10");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Game20", html);
        Assert.DoesNotContain("Game10", html);
        Assert.Equal(20, Assert.Single(store.RequestedAppIds));
        Assert.Equal(1, steam.LibraryCalls);
        Assert.Equal(0, outgoing.CallCount);
        Assert.Single(telemetry.Picks);
        // Persisted prerender data remains available for InteractiveAuto hydration.
        Assert.Contains("Blazor-WebAssembly-Component-State:", html);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StoreDependencyFailure_ReturnsControlledResultForHttpAndServerCalls(bool serverCall)
    {
        using var root = new SeoWebApplicationFactory();
        var store = new StubStoreClient(_ => Task.FromException<Store.AppData?>(new HttpRequestException("Store unavailable.")));
        using var application = CreateApplication(root, new StubSteamClient(10, 20), store, new RejectOutgoingHttpHandler());

        if (serverCall)
        {
            using var scope = application.Services.CreateScope();
            var server = scope.ServiceProvider.GetRequiredService<IRandomSteamApiClient>();
            var result = await server.GetRandomGameDetailsAsync("steam", SteamId, cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(result.IsSuccess);
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
            Assert.Equal(Errors.Steam.SteamApiFailed.Description, result.Problem!.Detail);
        }
        else
        {
            using var browser = application.CreateClient();
            using var response = await browser.GetAsync($"/api/steam/random-game/details?userId={SteamId}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ApiProblem>(TestContext.Current.CancellationToken);
            Assert.Equal(Errors.Steam.SteamApiFailed.Description, problem!.Detail);
        }

        Assert.Single(store.RequestedAppIds);
    }

    [Fact]
    public async Task ServerPicker_ForwardsCallerCancellationToStoreAndPropagatesIt()
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var root = new SeoWebApplicationFactory();
        var store = new StubStoreClient(ct =>
        {
            Assert.Equal(caller.Token, ct);
            caller.Cancel();
            return Task.FromCanceled<Store.AppData?>(ct);
        });
        using var application = CreateApplication(root, new StubSteamClient(10, 20), store, new RejectOutgoingHttpHandler());
        using var scope = application.Services.CreateScope();
        var server = scope.ServiceProvider.GetRequiredService<IRandomSteamApiClient>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            server.GetRandomGameDetailsAsync("steam", SteamId, cancellationToken: caller.Token));

        Assert.Single(store.RequestedAppIds);
    }

    [Fact]
    public async Task ServerScopes_KeepRequestCookiesSeparateAndAcceptCurrentBrowserExclusions()
    {
        using var root = new SeoWebApplicationFactory();
        var steam = new StubSteamClient(10);
        using var application = CreateApplication(root, steam, new StubStoreClient(), new RejectOutgoingHttpHandler());
        var accessor = application.Services.GetRequiredService<IHttpContextAccessor>();
        using var blockedScope = application.Services.CreateScope();
        using var otherScope = application.Services.CreateScope();
        try
        {
            accessor.HttpContext = RequestContext("ExcludedGameIds=10");
            var blockedClient = blockedScope.ServiceProvider.GetRequiredService<IRandomSteamApiClient>();
            accessor.HttpContext = RequestContext("ExcludedGameIds=");
            var otherClient = otherScope.ServiceProvider.GetRequiredService<IRandomSteamApiClient>();
            accessor.HttpContext = null; // Circuit events do not require a live request.

            Assert.IsType<ServerRandomSteamApiClient>(blockedClient);
            var ct = TestContext.Current.CancellationToken;
            var initiallyBlocked = await blockedClient.GetRandomGameDetailsAsync("steam", SteamId, cancellationToken: ct);
            Assert.Equal(HttpStatusCode.NotFound, initiallyBlocked.StatusCode);
            Assert.True((await otherClient.GetRandomGameDetailsAsync("steam", SteamId, cancellationToken: ct)).IsSuccess);

            // An empty browser list overrides the stale initial request cookie.
            var cleared = await blockedClient.GetRandomGameDetailsAsync("steam", SteamId, cancellationToken: ct, excludedGameIds: []);
            Assert.True(cleared.IsSuccess);
            Assert.Equal(10, cleared.Value!.Id);
            var newlyBlocked = await otherClient.GetRandomGameDetailsAsync("steam", SteamId, cancellationToken: ct, excludedGameIds: [10]);
            Assert.Equal(HttpStatusCode.NotFound, newlyBlocked.StatusCode);
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    [Fact]
    public async Task DirectCallsAndHttpEndpoints_ShareOneAdmissionWindow()
    {
        using var root = new SeoWebApplicationFactory();
        using var application = CreateApplication(root, new StubSteamClient(10), new StubStoreClient(),
            new RejectOutgoingHttpHandler(), permitLimit: 2);
        using var scope = application.Services.CreateScope();
        var server = scope.ServiceProvider.GetRequiredService<IRandomSteamApiClient>();
        using var browser = application.CreateClient();
        var limiter = application.Services.GetRequiredService<SteamApiRequestLimiter>();
        Assert.Equal(2, limiter.GetStatistics()!.CurrentAvailablePermits);

        var ct = TestContext.Current.CancellationToken;
        Assert.True((await server.GetOwnedGamesAsync("steam", SteamId, ct)).IsSuccess);
        using var accepted = await browser.GetAsync($"/api/steam/{SteamId}/library", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.True((await server.GetRandomGameDetailsAsync("steam", SteamId, cancellationToken: ct)).IsTooManyRequests);
        using var rejected = await browser.GetAsync($"/api/steam/{SteamId}/library", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    [Fact]
    public void SharedAdmissionWindow_ReportsNoIdleDuration()
    {
        using var limiter = new SteamApiRequestLimiter(new SteamApiClient.Settings.RateLimitingOptions
        {
            PermitLimit = 2, WindowSeconds = 1
        });
        Assert.Null(limiter.IdleDuration);
        using var lease = limiter.AttemptAcquire();
        Assert.True(lease.IsAcquired);
        Assert.Null(limiter.IdleDuration);
    }

    [Fact]
    public async Task SharedAdmissionWindow_AsyncDisposalDisposesInnerLimiter()
    {
        await using var limiter = new SteamApiRequestLimiter(new SteamApiClient.Settings.RateLimitingOptions
        {
            PermitLimit = 2, WindowSeconds = 1
        });

        using var lease = limiter.AttemptAcquire();
        Assert.True(lease.IsAcquired);
        await limiter.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => limiter.AttemptAcquire());
    }

    [Fact]
    public async Task ServerAdapter_PreservesVanityLibraryAndRefreshOperations()
    {
        using var root = new SeoWebApplicationFactory();
        var steam = new StubSteamClient(10);
        using var application = CreateApplication(root, steam, new StubStoreClient(), new RejectOutgoingHttpHandler());
        using var scope = application.Services.CreateScope();
        var server = scope.ServiceProvider.GetRequiredService<IRandomSteamApiClient>();

        var ct = TestContext.Current.CancellationToken;
        Assert.Equal(SteamId, (await server.ResolveVanityUrlAsync("steam", "some_vanity", ct)).Value);
        Assert.Single((await server.GetOwnedGamesAsync("steam", SteamId, ct)).Value!.Games);
        Assert.Equal(HttpStatusCode.BadRequest, (await server.GetRandomGameDetailsAsync("steam", 12, cancellationToken: ct)).StatusCode);
        Assert.True((await server.InvalidateOwnedGamesCacheAsync("steam", SteamId, ct)).IsSuccess);
        var cooldown = await server.InvalidateOwnedGamesCacheAsync("steam", SteamId, ct);
        Assert.True(cooldown.IsTooManyRequests);
        Assert.Equal("TooManyRequests", cooldown.Problem!.Title);
        Assert.Equal(1, steam.InvalidationCalls);
        Assert.True((await server.GetStatsAsync(ct)).IsSuccess);
    }

    [Theory]
    [InlineData("http://browser.b32.i2p/")]
    [InlineData("https://browser-public.invalid/")]
    public async Task BrowserRegistration_KeepsHttpRequestsOnItsConfiguredOrigin(string origin)
    {
        var handler = new RecordingBrowserHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRandomSteamBrowserApiClient(origin);
        services.AddHttpClient<IRandomSteamApiClient, RandomSteamApiClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IRandomSteamApiClient>();

        Assert.IsType<RandomSteamApiClient>(client);
        Assert.True((await client.GetRandomGameDetailsAsync("steam", SteamId, cancellationToken: TestContext.Current.CancellationToken)).IsSuccess);
        Assert.Equal(new Uri(new Uri(origin), $"api/steam/random-game/details?userId={SteamId}"), handler.Destination);
    }

    [Fact]
    public void HttpClient_RequiresAnExplicitBaseAddress()
    {
        using var http = new HttpClient();
        Assert.Throws<ArgumentException>(() => new RandomSteamApiClient(http, NullLogger<RandomSteamApiClient>.Instance));
    }

    private static DefaultHttpContext RequestContext(string cookie)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = cookie;
        context.Request.Host = new HostString("unreachable.b32.i2p");
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.42");
        return context;
    }

    private static WebApplicationFactory<Program> CreateApplication(SeoWebApplicationFactory root,
        StubSteamClient steam, StubStoreClient store, RejectOutgoingHttpHandler outgoing,
        RecordingTelemetry? telemetry = null, string origin = "http://unreachable.b32.i2p",
        string mode = "AltNet", int permitLimit = 50) => root.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Application:NetworkMode"] = mode,
            ["Application:CanonicalOrigin"] = mode == "Public" ? "https://configured-public.invalid" : origin,
            ["Steam:RateLimiting:PermitLimit"] = permitLimit.ToString(),
            ["Steam:RateLimiting:WindowSeconds"] = "3600"
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGameProvider>();
            services.AddScoped<IGameProvider, SteamProvider>();
            services.RemoveAll<ISteamClient>();
            services.AddSingleton<ISteamClient>(steam);
            services.RemoveAll<ISteamStoreClient>();
            services.AddSingleton<ISteamStoreClient>(store);
            // The host captures these settings before WebApplicationFactory's configuration callbacks.
            services.RemoveAll<SteamApiRequestLimiter>();
            services.AddSingleton(_ => new SteamApiRequestLimiter(new SteamApiClient.Settings.RateLimitingOptions
            {
                PermitLimit = permitLimit, WindowSeconds = 3600
            }));
            if (telemetry is not null)
            {
                services.RemoveAll<IMissionControlClient>();
                services.AddSingleton<IMissionControlClient>(telemetry);
            }
            services.ConfigureAll<HttpClientFactoryOptions>(options =>
                options.HttpMessageHandlerBuilderActions.Add(handler => handler.PrimaryHandler = outgoing));
        });
    });

    private sealed class StubSteamClient(params int[] appIds) : ISteamClient
    {
        public int LibraryCalls { get; private set; }
        public int InvalidationCalls { get; private set; }
        public Action<string, CancellationToken>? OnOperation { get; init; }
        public Task<Sdk.OwnedGames> GetOwnedGames(long steamId, bool includeAppInfo = true,
            bool includePlayedFreeGames = true, CancellationToken ct = default)
        {
            LibraryCalls++;
            OnOperation?.Invoke("library", ct);
            return Task.FromResult(new Sdk.OwnedGames(appIds.Length,
                appIds.Select(id => new Sdk.Game(id, $"Game{id}", 0, null, 0, 0, 0, 0, 0)).ToList()));
        }
        public async Task<OwnedGamesResult> GetOwnedGamesWithCacheInfo(long steamId, bool includeAppInfo = true,
            bool includePlayedFreeGames = true, CancellationToken ct = default) =>
            new(await GetOwnedGames(steamId, includeAppInfo, includePlayedFreeGames, ct), OwnedGamesCacheInfo.Unknown);
        public Task<long> GetSteamIdFromVanityUrl(string vanityUrl, CancellationToken ct = default)
        {
            OnOperation?.Invoke("vanity", ct);
            return Task.FromResult(SteamId);
        }
        public Task<IReadOnlyDictionary<int, Sdk.SteamDeckCompatibilityCategory>> GetSteamDeckCompatibilityAsync(
            IEnumerable<int> appIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, Sdk.SteamDeckCompatibilityCategory>>(new Dictionary<int, Sdk.SteamDeckCompatibilityCategory>());
        public Task InvalidateOwnedGamesCacheAsync(long steamId, CancellationToken ct = default)
        {
            InvalidationCalls++;
            OnOperation?.Invoke("refresh", ct);
            return Task.CompletedTask;
        }
    }

    private sealed class StubStoreClient(Func<CancellationToken, Task<Store.AppData?>>? getAppData = null) : ISteamStoreClient
    {
        public List<int> RequestedAppIds { get; } = [];
        public Task<Store.AppData?> GetAppData(int appId, IEnumerable<string>? tags = null, CancellationToken ct = default)
        {
            RequestedAppIds.Add(appId);
            if (getAppData is not null)
            {
                return getAppData(ct);
            }
            return Task.FromResult(JsonSerializer.Deserialize<Store.AppData>(JsonSerializer.Serialize(new
            {
                type = "game", name = $"Game{appId}", steam_appid = appId,
                about_the_game = "A game description.", header_image = ""
            })));
        }
    }

    private sealed class RejectOutgoingHttpHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            throw new InvalidOperationException($"Unexpected outbound HTTP: {request.RequestUri}");
        }
    }

    private sealed class RecordingBrowserHandler : HttpMessageHandler
    {
        public Uri? Destination { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Destination = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new GameDetails { Id = 10, Name = "Game10" })
            });
        }
    }

    private sealed class RecordingTelemetry : IMissionControlClient
    {
        public List<GamePickCompletedEvent> Picks { get; } = [];
        public Task<bool> TryPublishAsync<TPayload>(string eventType, TPayload payload,
            JsonTypeInfo<TPayload> payloadTypeInfo, DateTimeOffset occurredAt,
            string? correlationId = null, CancellationToken cancellationToken = default)
        {
            if (payload is GamePickCompletedEvent pick) Picks.Add(pick);
            return Task.FromResult(true);
        }
    }
}

// These hosts initialize/clean up the same startup SQLite file as other HTTP fixtures.
[CollectionDefinition(nameof(ServerBrowserExecutionTests), DisableParallelization = true)]
public sealed class ServerBrowserExecutionCollection;
