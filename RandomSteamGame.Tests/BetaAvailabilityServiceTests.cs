using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using RandomSteamGame.Options;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace RandomSteamGame.Tests;

public sealed class BetaAvailabilityServiceTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.Redirect, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    public async Task SeparateRequestScopes_ShareOneProbeAndCachedBool(HttpStatusCode status, bool expected)
    {
        using var handler = new ControlledHttpFactory();
        using var fixture = new SeoWebApplicationFactory();
        using var factory = CreateBetaWebApplicationFactory(fixture, handler);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IBetaAvailabilityService>();
        var second = secondScope.ServiceProvider.GetRequiredService<IBetaAvailabilityService>();
        Assert.Same(first, second);

        var release = new TaskCompletionSource<HttpStatusCode>(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.Probe = (_, token) => release.Task.WaitAsync(token);
        var requests = Enumerable.Range(0, 8).Select(i =>
            (i % 2 == 0 ? first : second).IsBetaAvailableAsync(TestContext.Current.CancellationToken)).ToArray();
        Assert.Equal(1, handler.RequestCount);
        Assert.All(requests, request => Assert.False(request.IsCompleted));
        release.SetResult(status);
        Assert.All(await Task.WhenAll(requests), available => Assert.Equal(expected, available));
        Assert.Equal(expected, await second.IsBetaAvailableAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task Expiration_AllowsOneNewProbeForNextConcurrentWave()
    {
        var clock = new CacheClock();
#pragma warning disable CS0618
        using var cache = new MemoryCache(new MemoryCacheOptions { Clock = clock });
#pragma warning restore CS0618
        using var handler = new ControlledHttpFactory();
        using var service = CreateService(cache, handler);
        var ct = TestContext.Current.CancellationToken;
        Assert.True(await service.IsBetaAvailableAsync(ct));
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.True(await service.IsBetaAvailableAsync(ct));
        Assert.Equal(1, handler.RequestCount);
        clock.Advance(TimeSpan.FromSeconds(2));

        var release = new TaskCompletionSource<HttpStatusCode>(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.Probe = (_, token) => release.Task.WaitAsync(token);
        var requests = Enumerable.Range(0, 8).Select(_ => service.IsBetaAvailableAsync(ct)).ToArray();
        Assert.Equal(2, handler.RequestCount);
        release.SetResult(HttpStatusCode.ServiceUnavailable);
        Assert.All(await Task.WhenAll(requests), Assert.False);
        Assert.False(await service.IsBetaAvailableAsync(ct));
        Assert.Equal(2, handler.RequestCount);
    }

    [Theory]
    [InlineData(NetworkMode.AltNet, null, false)]
    [InlineData(NetworkMode.Public, false, false)]
    [InlineData(NetworkMode.Public, null, true)]
    [InlineData(NetworkMode.AltNet, true, true)]
    public async Task ModeAndOverride_ArePreservedUnderConcurrentCalls(NetworkMode mode, bool? enabled, bool expected)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var handler = new ControlledHttpFactory();
        using var service = CreateService(cache, handler, new ApplicationOptions
        {
            NetworkMode = mode, EnableBetaProbe = enabled
        });
        // Disabled deployments must ignore even an existing availability entry.
        if (!expected) cache.Set("beta-picker-availability", true);
        var requests = Enumerable.Range(0, 8).Select(_ =>
            service.IsBetaAvailableAsync(TestContext.Current.CancellationToken));
        Assert.All(await Task.WhenAll(requests), result => Assert.Equal(expected, result));
        Assert.Equal(expected ? 1 : 0, handler.RequestCount);
        Assert.Equal(expected ? 1 : 0, handler.CreateCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NetworkFailureOrDependencyTimeout_CachesFalseAndReleasesGate(bool timeout)
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var handler = new ControlledHttpFactory();
        using var service = CreateService(cache, handler);
        // Surface the same exception as an internally canceled HTTP operation without a real-time wait.
        handler.Probe = (_, _) => throw (timeout
            ? new OperationCanceledException("Dependency timeout")
            : new HttpRequestException("Network unavailable"));
        Assert.False(await service.IsBetaAvailableAsync(TestContext.Current.CancellationToken));
        Assert.False(await service.IsBetaAvailableAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, handler.RequestCount);
        cache.Remove("beta-picker-availability");
        handler.Probe = (_, _) => Task.FromResult(HttpStatusCode.OK);
        Assert.True(await service.IsBetaAvailableAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task WaitingCallerCancellation_DoesNotCancelProbeOwner()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var handler = new ControlledHttpFactory();
        using var service = CreateService(cache, handler);
        var release = new TaskCompletionSource<HttpStatusCode>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken ownerToken = default;
        handler.Probe = (_, token) => { ownerToken = token; return release.Task.WaitAsync(token); };
        var owner = service.IsBetaAvailableAsync(TestContext.Current.CancellationToken);
        using var waiterCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var waiter = service.IsBetaAvailableAsync(waiterCancellation.Token);
        Assert.False(waiter.IsCompleted);
        waiterCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
        Assert.False(ownerToken.IsCancellationRequested);
        Assert.False(owner.IsCompleted);
        release.SetResult(HttpStatusCode.OK);
        Assert.True(await owner);
        Assert.True(await service.IsBetaAvailableAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, handler.RequestCount);
        cache.Remove("beta-picker-availability");
        Assert.True(await service.IsBetaAvailableAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task ProbeOwnerCancellation_DoesNotCacheFalseAndWaitingCallerCanRetry()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var handler = new ControlledHttpFactory();
        using var service = CreateService(cache, handler);
        var release = new TaskCompletionSource<HttpStatusCode>(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.Probe = (count, token) => count == 1
            ? release.Task.WaitAsync(token) : Task.FromResult(HttpStatusCode.OK);
        using var ownerCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var owner = service.IsBetaAvailableAsync(ownerCancellation.Token);
        var waiter = service.IsBetaAvailableAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, handler.RequestCount);
        ownerCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner);
        Assert.True(await waiter);
        Assert.True(await service.IsBetaAvailableAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, handler.RequestCount);
    }

    private static BetaAvailabilityService CreateService(IMemoryCache cache, IHttpClientFactory handler,
        ApplicationOptions? options = null) => new(cache, handler,
            NullLogger<BetaAvailabilityService>.Instance, OptionsFactory.Create(options ?? new ApplicationOptions()));

    private sealed class ControlledHttpFactory : HttpMessageHandler, IHttpClientFactory
    {
        private int _requests;
        private int _clients;
        public int RequestCount => Volatile.Read(ref _requests);
        public int CreateCount => Volatile.Read(ref _clients);
        public Func<int, CancellationToken, Task<HttpStatusCode>> Probe { get; set; } =
            (_, _) => Task.FromResult(HttpStatusCode.OK);
        public HttpClient CreateClient(string name)
        {
            Interlocked.Increment(ref _clients);
            return new HttpClient(this, disposeHandler: false);
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal("https://randombeta.kgivler.com/api/stats", request.RequestUri?.AbsoluteUri);
            return new HttpResponseMessage(await Probe(Interlocked.Increment(ref _requests), token));
        }
    }

    private static WebApplicationFactory<Program> CreateBetaWebApplicationFactory(
        SeoWebApplicationFactory fixture, ControlledHttpFactory handler) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Reuse the fixture's isolated configuration, but retain real application registrations.
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddConfiguration(fixture.Services.GetRequiredService<IConfiguration>()));
            builder.ConfigureTestServices(services =>
            {
                var registration = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IBetaAvailabilityService));
                Assert.Equal(ServiceLifetime.Singleton, registration.Lifetime);
                Assert.Equal(typeof(BetaAvailabilityService), registration.ImplementationType);
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(handler);
            });
        });

#pragma warning disable CS0618
    private sealed class CacheClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.UtcNow;
        public void Advance(TimeSpan duration) => UtcNow += duration;
    }
#pragma warning restore CS0618
}
