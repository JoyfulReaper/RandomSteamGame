using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using NSubstitute;
using SteamApiClient.Services;
using SteamApiClient.Settings;

namespace SteamApiClient.Tests;

public partial class CacheServiceIntegrationTests
{
    [Theory]
    [InlineData("long")]
    [InlineData("bool")]
    [InlineData("false")]
    [InlineData("timestamp")]
    [InlineData("nullable-long")]
    [InlineData("nullable-bool")]
    [InlineData("nullable-timestamp")]
    [InlineData("reference")]
    public Task CacheOnlyRead_MissesDoNotPoisonAndL2HitsWarmL1(string type) => type switch
    {
        "long" => VerifyCacheOnlyReadAsync(76561198000000001L),
        "bool" => VerifyCacheOnlyReadAsync(true),
        "false" => VerifyCacheOnlyReadAsync(false),
        "timestamp" => VerifyCacheOnlyReadAsync(DateTimeOffset.Parse("2026-10-09T12:00:00Z")),
        "nullable-long" => VerifyCacheOnlyReadAsync<long?>(76561198000000001L),
        "nullable-bool" => VerifyCacheOnlyReadAsync<bool?>(true),
        "nullable-timestamp" => VerifyCacheOnlyReadAsync<DateTimeOffset?>(DateTimeOffset.Parse("2026-10-09T12:00:00Z")),
        _ => VerifyCacheOnlyReadAsync(new CacheReference("SteamData", 42))
    };

    private static async Task VerifyCacheOnlyReadAsync<T>(T value)
    {
        var l2 = new CountedL2();
        using var a = CreateCacheProvider(l2);
        using var b = CreateCacheProvider(l2);
        var writer = a.GetRequiredService<ICacheService>();
        var reader = b.GetRequiredService<ICacheService>();
        const string key = "cache-only";
        var ct = TestContext.Current.CancellationToken;

        Assert.Equal(default, await reader.GetAsync<T>(key, ct));
        Assert.Equal(default, await reader.GetAsync<T>(key, ct));
        Assert.Equal(2, l2.Reads.GetValueOrDefault(key));
        Assert.False(l2.Entries.ContainsKey(key));
        Assert.Equal(0, l2.Writes.GetValueOrDefault(key));

        await writer.SetAsync(key, value, new CachePolicy { AbsoluteMinutes = 60 }, ct: ct);
        Assert.Equal(value, await reader.GetAsync<T>(key, ct));
        Assert.Equal(3, l2.Reads[key]);
        Assert.Equal(value, await reader.GetAsync<T>(key, ct));
        Assert.Equal(3, l2.Reads[key]);
        Assert.Equal(1, l2.Writes[key]);
    }

    [Fact]
    public async Task CacheOnlyRead_L2PromotionUsesFiveMinuteLocalLifetime()
    {
        var clock = new CacheClock();
        var l2 = new CountedL2();
        using var a = CreateCacheProvider(l2, clock);
        using var b = CreateCacheProvider(l2, clock);
        var reader = b.GetRequiredService<ICacheService>();
        var ct = TestContext.Current.CancellationToken;
        const string key = "promotion-lifetime";
        await a.GetRequiredService<ICacheService>().SetAsync(key, "value",
            new CachePolicy { AbsoluteMinutes = 60 }, ct: ct);

        Assert.Equal("value", await reader.GetAsync<string>(key, ct));
        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.Equal("value", await reader.GetAsync<string>(key, ct));
        Assert.Equal(1, l2.Reads[key]);
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal("value", await reader.GetAsync<string>(key, ct));
        Assert.Equal(2, l2.Reads[key]);
        Assert.Equal(1, l2.Writes[key]);
    }

    [Fact]
    public async Task CacheOnlyRead_CanceledL2ReadPropagatesWithoutPoisoningCache()
    {
        const string key = "canceled-read";
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var l2 = new CountedL2();
        l2.BeforeRead = async (readKey, token) =>
        {
            if (readKey != key) return;
            started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally { stopped.TrySetResult(); }
        };
        using var a = CreateCacheProvider(l2);
        using var b = CreateCacheProvider(l2);
        var reader = b.GetRequiredService<ICacheService>();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = reader.GetAsync<long?>(key, cancellation.Token);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await stopped.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(l2.Entries.ContainsKey(key));
        Assert.Equal(0, l2.Writes.GetValueOrDefault(key));

        l2.BeforeRead = null;
        await a.GetRequiredService<ICacheService>().SetAsync<long?>(key, 42,
            new CachePolicy { AbsoluteMinutes = 60 }, ct: TestContext.Current.CancellationToken);
        Assert.Equal(42L, await reader.GetAsync<long?>(key, TestContext.Current.CancellationToken));
        Assert.Equal(1, l2.Writes[key]);
    }

    private static ServiceProvider CreateCacheProvider(CountedL2 l2, CacheClock? clock = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(l2.Cache);
        if (clock is not null)
        {
            services.AddSingleton<TimeProvider>(clock);
#pragma warning disable CS0618 // Synchronize MemoryCache and HybridCache clocks for deterministic expiry.
            services.AddMemoryCache(options => options.Clock = clock);
#pragma warning restore CS0618
        }
        services.AddHybridCache(options => options.DefaultEntryOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromHours(1),
            LocalCacheExpiration = TimeSpan.FromMinutes(30)
        });
        services.AddScoped<ICacheService, CacheService>();
        return services.BuildServiceProvider();
    }

    public sealed record CacheReference(string Name, int Count);

#pragma warning disable CS0618
    private sealed class CacheClock : TimeProvider, ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
        public void Advance(TimeSpan duration) => UtcNow += duration;
    }
#pragma warning restore CS0618

    private sealed class CountedL2
    {
        public IDistributedCache Cache { get; } = Substitute.For<IDistributedCache>();
        public ConcurrentDictionary<string, byte[]> Entries { get; } = new();
        public ConcurrentDictionary<string, int> Reads { get; } = new();
        public ConcurrentDictionary<string, int> Writes { get; } = new();
        public Func<string, CancellationToken, Task>? BeforeRead { get; set; }

        public CountedL2()
        {
            Cache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
            {
                var key = call.ArgAt<string>(0);
                Reads.AddOrUpdate(key, 1, (_, count) => count + 1);
                if (BeforeRead is { } beforeRead) await beforeRead(key, call.ArgAt<CancellationToken>(1));
                return Entries.GetValueOrDefault(key);
            });
            Cache.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(),
                Arg.Any<CancellationToken>()).Returns(call =>
            {
                var key = call.ArgAt<string>(0);
                Entries[key] = call.ArgAt<byte[]>(1).ToArray();
                Writes.AddOrUpdate(key, 1, (_, count) => count + 1);
                return Task.CompletedTask;
            });
        }
    }
}
