using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;
using SteamApiClient.HttpClients;
using SteamApiClient.Services;
using SteamApiClient.Settings;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Threading.Channels;

namespace SteamApiClient.Tests;

public sealed class CacheMissCoalescingTests
{
    private const long SteamId = 76561197960287930;
    private const string VanitySuccess = "{\"response\":{\"success\":1,\"steamid\":\"76561197960287930\"}}";
    private const string VanityNotFound = "{\"response\":{\"success\":42}}";
    private const string StoreSuccess = "{\"400\":{\"success\":true,\"data\":{\"name\":\"Portal\",\"steam_appid\":400}}}";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Vanity_NormalizedAliasesShareFillAndKeepSeparatePolicies(bool noMatch)
    {
        using var handler = new GatedHandler((_, _) => Json(noMatch ? VanityNotFound : VanitySuccess));
        using var fixture = new Clients(handler);
        string[] aliases = ["Example_Name", "example_name", "https://steamcommunity.com/id/EXAMPLE_NAME/", "/example_name/"];
        var requests = Enumerable.Range(0, 24).Select(i =>
            (i % 2 == 0 ? fixture.Vanity : fixture.OtherVanity).GetSteamIdFromVanityUrl(aliases[i % aliases.Length], Ct)).ToArray();
        await handler.NextRequestAsync();
        Assert.Equal(1, handler.CallCount);
        Assert.All(requests, request => Assert.False(request.IsCompleted));
        handler.Release();

        Assert.All(await Task.WhenAll(requests), result => Assert.Equal(noMatch ? 0 : SteamId, result));
        Assert.Equal(noMatch ? 0 : SteamId, await fixture.OtherVanity.GetSteamIdFromVanityUrl(aliases[2], Ct));
        Assert.Equal(1, handler.CallCount);
        var key = noMatch ? "vanity:v2:notfound:example_name" : "vanity:v2:success:example_name";
        var oppositeKey = noMatch ? "vanity:v2:success:example_name" : "vanity:v2:notfound:example_name";
        Assert.True(fixture.Entries.ContainsKey(key));
        Assert.False(fixture.Entries.ContainsKey(oppositeKey));
        Assert.Equal(TimeSpan.FromMinutes(noMatch ? 15 : 120), fixture.Expirations[key]);
        Assert.DoesNotContain(fixture.Entries.Keys, key => key.Contains(":fill:"));

        // Both existing invalidation tags still apply to either representation.
        foreach (var tag in new[] { "vanity:v2:success:example_name", "vanity_urls" })
        {
            await fixture.Cache.InvalidateByTagAsync(tag, Ct);
            Assert.Equal(noMatch ? 0 : SteamId, await fixture.Vanity.GetSteamIdFromVanityUrl(aliases[0], Ct));
        }
        Assert.Equal(3, handler.CallCount);
    }

    [Theory]
    [InlineData("http500")]
    [InlineData("transport")]
    [InlineData("timeout")]
    [InlineData("circuit")]
    [InlineData("ratelimit")]
    [InlineData("json")]
    [InlineData("shape")]
    [InlineData("status")]
    public async Task Vanity_FailedSharedFillIsNotCachedAndCanRetry(string failure)
    {
        using var handler = new GatedHandler((_, call) => call == 1 ? Failure(failure) : Json(VanitySuccess));
        using var fixture = new Clients(handler);
        var requests = Enumerable.Range(0, 12).Select(_ => fixture.Vanity.GetSteamIdFromVanityUrl("example_name", Ct)).ToArray();
        await handler.NextRequestAsync();
        Assert.Equal(1, handler.CallCount);
        handler.Release();
        foreach (var request in requests)
        {
            AssertDependencyFailure(failure, await Record.ExceptionAsync(async () => await request));
        }
        Assert.False(fixture.Entries.ContainsKey("vanity:v2:success:example_name"));
        Assert.False(fixture.Entries.ContainsKey("vanity:v2:notfound:example_name"));
        Assert.Equal(SteamId, await fixture.OtherVanity.GetSteamIdFromVanityUrl("example_name", Ct));
        Assert.Equal(2, handler.CallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Store_SharedFillCachesOnlySuccessOrExplicitAbsence(bool unavailable)
    {
        using var handler = new GatedHandler((_, _) => Json(unavailable ? "{\"400\":{\"success\":false}}" : StoreSuccess));
        using var fixture = new Clients(handler);
        var requests = Enumerable.Range(0, 24).Select(i =>
            (i % 2 == 0 ? fixture.Store : fixture.OtherStore).GetAppData(400, ["custom_tag"], Ct)).ToArray();
        await handler.NextRequestAsync();
        Assert.Equal(1, handler.CallCount);
        Assert.All(requests, request => Assert.False(request.IsCompleted));
        handler.Release();
        foreach (var result in await Task.WhenAll(requests))
        {
            if (unavailable) Assert.Null(result);
            else Assert.Equal("Portal", Assert.IsType<Contracts.SteamStoreApi.AppData>(result).Name);
        }
        var cached = await fixture.OtherStore.GetAppData(400, ct: Ct);
        Assert.Equal(unavailable, cached is null);
        Assert.Equal(1, handler.CallCount);
        var key = unavailable ? "app:v1:notfound:400" : "app:400";
        Assert.True(fixture.Entries.ContainsKey(key));
        Assert.False(fixture.Entries.ContainsKey(unavailable ? "app:400" : "app:v1:notfound:400"));
        Assert.Equal(TimeSpan.FromMinutes(unavailable ? 15 : 60), fixture.Expirations[key]);
        Assert.DoesNotContain(fixture.Entries.Keys, key => key.Contains(":fill:"));

        foreach (var tag in new[] { "custom_tag", "app_400", "app_details" })
        {
            await fixture.Cache.InvalidateByTagAsync(tag, Ct);
            await fixture.Store.GetAppData(400, ["custom_tag"], Ct);
        }
        Assert.Equal(4, handler.CallCount);
    }

    [Theory]
    [InlineData("http500")]
    [InlineData("http429")]
    [InlineData("http404")]
    [InlineData("transport")]
    [InlineData("timeout")]
    [InlineData("circuit")]
    [InlineData("ratelimit")]
    [InlineData("json")]
    [InlineData("shape")]
    [InlineData("missingSuccess")]
    [InlineData("missingData")]
    [InlineData("null")]
    [InlineData("incompatible")]
    [InlineData("array")]
    [InlineData("malformedData")]
    public async Task Store_AmbiguousFailureIsNotNegativeCachedAndCanRetry(string failure)
    {
        using var handler = new GatedHandler((_, call) => call == 1 ? Failure(failure) : Json(StoreSuccess));
        using var fixture = new Clients(handler);
        var requests = Enumerable.Range(0, 12).Select(_ => fixture.Store.GetAppData(400, ct: Ct)).ToArray();
        await handler.NextRequestAsync();
        Assert.Equal(1, handler.CallCount);
        handler.Release();
        foreach (var request in requests)
        {
            if (failure.StartsWith("http") || failure is "shape" or "missingSuccess" or "missingData" or "null")
                Assert.Null(await request);
            else
                AssertDependencyFailure(failure, await Record.ExceptionAsync(async () => await request));
        }
        Assert.False(fixture.Entries.ContainsKey("app:400"));
        Assert.False(fixture.Entries.ContainsKey("app:v1:notfound:400"));
        Assert.Equal("Portal", (await fixture.OtherStore.GetAppData(400, ct: Ct))?.Name);
        Assert.Equal(2, handler.CallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelingFirstWaiterDoesNotCancelAnotherLiveWaiter(bool store)
    {
        using var handler = new GatedHandler((_, _) => Json(store ? StoreSuccess : VanitySuccess));
        using var fixture = new Clients(handler);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var first = fixture.LookupAsync(store, "example_name", 400, caller.Token);
        await handler.NextRequestAsync();
        var live = fixture.LookupAsync(store, "EXAMPLE_NAME", 400, Ct, otherClient: true);
        caller.Cancel();
        var cancellation = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(TimeSpan.FromSeconds(10), Ct));
        Assert.Equal(caller.Token, cancellation.CancellationToken);
        Assert.False(handler.Canceled.Task.IsCompleted);
        Assert.False(live.IsCompleted);
        handler.Release();
        Assert.NotNull(await live);
        Assert.Equal(1, handler.CallCount);
        Assert.NotNull(await fixture.LookupAsync(store, "example_name", 400, Ct));
        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelingAllWaitersDoesNotCacheAbsenceOrPoisonLaterCalls(bool store)
    {
        using var handler = new GatedHandler((_, _) => Json(store ? StoreSuccess : VanitySuccess));
        using var fixture = new Clients(handler);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var canceled = fixture.LookupAsync(store, "example_name", 400, caller.Token);
        await handler.NextRequestAsync();
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled.WaitAsync(TimeSpan.FromSeconds(10), Ct));
        await handler.Canceled.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Assert.Empty(fixture.Entries);
        handler.Release();
        Assert.NotNull(await fixture.LookupAsync(store, "example_name", 400, Ct, otherClient: true));
        Assert.Equal(2, handler.CallCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DifferentKeysReachUpstreamIndependently(bool store)
    {
        using var handler = new GatedHandler((_, _) => Json(store ? "{}" : VanitySuccess));
        using var fixture = new Clients(handler);
        var first = fixture.LookupAsync(store, "first_name", 400, Ct);
        await handler.NextRequestAsync();
        var second = fixture.LookupAsync(store, "second_name", 620, Ct, otherClient: true);
        await handler.NextRequestAsync();
        Assert.Equal(2, handler.CallCount);
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        handler.Release();
        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task Coalesce_DoesNotPersistCompletedResults()
    {
        using var handler = new GatedHandler((_, _) => Json("{}"));
        using var fixture = new Clients(handler);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        async Task<int> Fill(CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult();
            await release.Task.WaitAsync(token);
            return calls;
        }
        var first = fixture.Cache.CoalesceAsync("test:fill", Fill, Ct);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        var second = fixture.Cache.CoalesceAsync("test:fill", Fill, Ct);
        release.SetResult();
        Assert.Equal(new[] { 1, 1 }, await Task.WhenAll(first, second));
        Assert.Equal(2, await fixture.Cache.CoalesceAsync("test:fill", Fill, Ct));
        Assert.Empty(fixture.Entries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreCanceledCallerDoesNotStartFill(bool store)
    {
        using var handler = new GatedHandler((_, _) => Json(store ? StoreSuccess : VanitySuccess));
        using var fixture = new Clients(handler);
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.LookupAsync(store, "example_name", 400, caller.Token));
        Assert.Equal(0, handler.CallCount);
        Assert.Empty(fixture.Entries);
    }

    [Fact]
    public async Task Store_NegativeCacheUsesConfiguredDuration()
    {
        using var handler = new GatedHandler((_, _) => Json("{\"400\":{\"success\":false}}"));
        handler.Release();
        using var fixture = new Clients(handler, negativeMinutes: 7);
        Assert.Null(await fixture.Store.GetAppData(400, ct: Ct));
        Assert.Null(await fixture.OtherStore.GetAppData(400, ct: Ct));
        Assert.Equal(TimeSpan.FromMinutes(7), fixture.Expirations["app:v1:notfound:400"]);
        Assert.Equal(1, handler.CallCount);
    }

    private static void AssertDependencyFailure(string failure, Exception? exception)
    {
        var expectedType = failure switch
        {
            "http500" or "transport" => typeof(HttpRequestException),
            "timeout" => typeof(TimeoutRejectedException),
            "circuit" => typeof(BrokenCircuitException),
            "ratelimit" => typeof(RateLimiterRejectedException),
            "json" or "incompatible" or "array" or "malformedData" => typeof(JsonException),
            _ => typeof(InvalidOperationException)
        };
        Assert.IsAssignableFrom(expectedType, exception);
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };

    private static HttpResponseMessage Failure(string failure) => failure switch
    {
        "http500" => new(HttpStatusCode.InternalServerError),
        "http429" => new(HttpStatusCode.TooManyRequests),
        "http404" => new(HttpStatusCode.NotFound),
        "transport" => throw new HttpRequestException("Test transport failure."),
        "timeout" => throw new TimeoutRejectedException(),
        "circuit" => throw new BrokenCircuitException(),
        "ratelimit" => throw new RateLimiterRejectedException(),
        "json" => Json("not JSON"),
        "shape" => Json("{}"),
        "status" => Json("{\"response\":{\"success\":3}}"),
        "missingSuccess" => Json("{\"400\":{}}"),
        "missingData" => Json("{\"400\":{\"success\":true}}"),
        "null" => Json("{\"400\":null}"),
        "incompatible" => Json("{\"400\":{\"success\":\"false\"}}"),
        "array" => Json("{\"400\":[]}"),
        "malformedData" => Json("{\"400\":{\"success\":false,\"data\":\"invalid\"}}"),
        _ => throw new ArgumentOutOfRangeException(nameof(failure))
    };

    private sealed class GatedHandler(Func<HttpRequestMessage, int, HttpResponseMessage> response) : HttpMessageHandler
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Channel<HttpRequestMessage> _entered = Channel.CreateUnbounded<HttpRequestMessage>();
        private int _calls;
        public int CallCount => Volatile.Read(ref _calls);
        public TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Release() => _release.TrySetResult();
        public Task<HttpRequestMessage> NextRequestAsync() => _entered.Reader.ReadAsync(Ct).AsTask().WaitAsync(TimeSpan.FromSeconds(10), Ct);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var call = Interlocked.Increment(ref _calls);
            _entered.Writer.TryWrite(request);
            try
            {
                await _release.Task.WaitAsync(ct);
                return response(request, call);
            }
            catch (OperationCanceledException)
            {
                Canceled.TrySetResult();
                throw;
            }
        }
    }

    private sealed class Clients : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;
        private readonly IServiceScope _otherScope;
        private readonly HttpClient _http;
        public ConcurrentDictionary<string, byte[]> Entries { get; } = new();
        public ConcurrentDictionary<string, TimeSpan?> Expirations { get; } = new();
        public ICacheService Cache { get; }
        public SteamClient Vanity { get; }
        public SteamClient OtherVanity { get; }
        public SteamStoreClient Store { get; }
        public SteamStoreClient OtherStore { get; }
        public Clients(HttpMessageHandler handler, int negativeMinutes = 15)
        {
            var distributed = Substitute.For<IDistributedCache>();
            distributed.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => Task.FromResult(Entries.GetValueOrDefault(call.ArgAt<string>(0))));
            distributed.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    Entries[call.ArgAt<string>(0)] = call.ArgAt<byte[]>(1).ToArray();
                    Expirations[call.ArgAt<string>(0)] = call.ArgAt<DistributedCacheEntryOptions>(2).AbsoluteExpirationRelativeToNow;
                    return Task.CompletedTask;
                });
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(distributed);
            services.AddHybridCache();
            services.AddScoped<ICacheService, CacheService>();
            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateScope();
            _otherScope = _provider.CreateScope();
            Cache = _scope.ServiceProvider.GetRequiredService<ICacheService>();
            var otherCache = _otherScope.ServiceProvider.GetRequiredService<ICacheService>();
            _http = new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("https://steam.test/") };
            var options = Options.Create(new SteamClientApiOptions
            {
                ApiKey = "FAKE_API_KEY",
                Cache = new CacheSettings
                {
                    AppDetails = new() { AbsoluteMinutes = 60 },
                    AppDetailsNotFound = new() { AbsoluteMinutes = negativeMinutes },
                    VanitySuccess = new() { AbsoluteMinutes = 120 },
                    VanityNotFound = new() { AbsoluteMinutes = 15 }
                }
            });
            Vanity = new(_http, options, Cache, NullLogger<SteamClient>.Instance);
            OtherVanity = new(_http, options, otherCache, NullLogger<SteamClient>.Instance);
            Store = new(_http, Cache, options, NullLogger<SteamStoreClient>.Instance);
            OtherStore = new(_http, otherCache, options, NullLogger<SteamStoreClient>.Instance);
        }
        public async Task<object?> LookupAsync(bool store, string vanity, int appId, CancellationToken ct, bool otherClient = false)
        {
            if (store) return await (otherClient ? OtherStore : Store).GetAppData(appId, ct: ct);
            return await (otherClient ? OtherVanity : Vanity).GetSteamIdFromVanityUrl(vanity, ct);
        }
        public void Dispose()
        {
            _http.Dispose();
            _otherScope.Dispose();
            _scope.Dispose();
            _provider.Dispose();
        }
    }
}
