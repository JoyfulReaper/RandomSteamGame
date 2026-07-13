/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.DependencyInjection;
using SteamApiClient.Services;
using SteamApiClient.Settings;

namespace SteamApiClient.Tests;

public class CacheServiceIntegrationTests
{
    private readonly ICacheService _cacheService;

    public CacheServiceIntegrationTests()
    {
        // DI container purely test
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHybridCache();
        services.AddScoped<ICacheService, CacheService>();

        var provider = services.BuildServiceProvider();
        _cacheService = provider.GetRequiredService<ICacheService>();
    }

    [Fact]
    public async Task GetOrCreateAsync_CacheMiss_ExecutesFactoryAndStoresValue()
    {
        // Arrange
        var key = "test_key";
        var policy = new CachePolicy { AbsoluteMinutes = 5 };
        var callCount = 0;

        Func<CancellationToken, Task<string>> factory = (token) =>
        {
            callCount++;
            return Task.FromResult("SteamData");
        };

        // Act - First call (Cache Miss)
        var result1 = await _cacheService.GetOrCreateAsync(
            key,
            factory,
            policy,
            ct: TestContext.Current.CancellationToken);

        // Act - Second call (Cache Hit)
        var result2 = await _cacheService.GetOrCreateAsync(
            key,
            factory,
            policy,
            ct: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("SteamData", result1);
        Assert.Equal("SteamData", result2);

        // The factory should have only executed ONCE because the second call was a cache hit!
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetOrCreateWithMetadataAsync_ConcurrentMiss_CoalescesFactoryExecution()
    {
        var key = $"metadata_concurrent_{Guid.NewGuid():N}";
        var policy = new CachePolicy { AbsoluteMinutes = 5 };
        var factoryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFactory = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCallCount = 0;

        async Task<string> Factory(CancellationToken token)
        {
            Interlocked.Increment(ref factoryCallCount);
            factoryStarted.SetResult();
            await releaseFactory.Task.WaitAsync(token);
            return "SteamData";
        }

        var tasks = Enumerable
            .Range(0, 8)
            .Select(_ => _cacheService.GetOrCreateWithMetadataAsync(
                key,
                Factory,
                policy,
                ct: TestContext.Current.CancellationToken))
            .ToArray();

        await factoryStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        releaseFactory.SetResult();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, factoryCallCount);
        Assert.All(results, result => Assert.Equal("SteamData", result.Value));
        Assert.Contains(results, result => result.Cache.Status == OwnedGamesCacheStatus.Miss);
    }

    [Fact]
    public async Task GetOrCreateWithMetadataAsync_SubsequentCall_ReturnsHit()
    {
        var key = $"metadata_hit_{Guid.NewGuid():N}";
        var policy = new CachePolicy { AbsoluteMinutes = 5 };
        var factoryCallCount = 0;

        async Task<string> Factory(CancellationToken token)
        {
            Interlocked.Increment(ref factoryCallCount);
            await Task.Yield();
            return "SteamData";
        }

        var miss = await _cacheService.GetOrCreateWithMetadataAsync(
            key,
            Factory,
            policy,
            ct: TestContext.Current.CancellationToken);
        var hit = await _cacheService.GetOrCreateWithMetadataAsync(
            key,
            Factory,
            policy,
            ct: TestContext.Current.CancellationToken);

        Assert.Equal("SteamData", miss.Value);
        Assert.Equal("SteamData", hit.Value);
        Assert.Equal(1, factoryCallCount);
        Assert.Equal(OwnedGamesCacheStatus.Hit, hit.Cache.Status);
        Assert.True(hit.Cache.AgeSeconds >= 0);
    }

    [Fact]
    public async Task GetOrCreateWithMetadataAsync_FactoryFailure_IsNotCached()
    {
        var key = $"metadata_failure_{Guid.NewGuid():N}";
        var policy = new CachePolicy { AbsoluteMinutes = 5 };
        var factoryCallCount = 0;

        async Task<string> Factory(CancellationToken token)
        {
            await Task.Yield();
            var count = Interlocked.Increment(ref factoryCallCount);
            if (count == 1)
            {
                throw new InvalidOperationException("Factory failed.");
            }

            return "RecoveredSteamData";
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _cacheService.GetOrCreateWithMetadataAsync(
                key,
                Factory,
                policy,
                ct: TestContext.Current.CancellationToken));

        var recovered = await _cacheService.GetOrCreateWithMetadataAsync(
            key,
            Factory,
            policy,
            ct: TestContext.Current.CancellationToken);
        var hit = await _cacheService.GetOrCreateWithMetadataAsync(
            key,
            Factory,
            policy,
            ct: TestContext.Current.CancellationToken);

        Assert.Equal(2, factoryCallCount);
        Assert.Equal("RecoveredSteamData", recovered.Value);
        Assert.Equal(OwnedGamesCacheStatus.Miss, recovered.Cache.Status);
        Assert.Equal("RecoveredSteamData", hit.Value);
        Assert.Equal(OwnedGamesCacheStatus.Hit, hit.Cache.Status);
    }

    [Fact]
    public async Task GetOrCreateWithMetadataAsync_TagInvalidation_CausesNextMiss()
    {
        var key = $"metadata_invalidation_{Guid.NewGuid():N}";
        var tag = $"tag_{Guid.NewGuid():N}";
        var policy = new CachePolicy { AbsoluteMinutes = 5 };
        var factoryCallCount = 0;

        Task<string> Factory(CancellationToken token)
        {
            var count = Interlocked.Increment(ref factoryCallCount);
            return Task.FromResult($"SteamData{count}");
        }

        var initial = await _cacheService.GetOrCreateWithMetadataAsync(
            key,
            Factory,
            policy,
            tags: [tag],
            ct: TestContext.Current.CancellationToken);

        await _cacheService.InvalidateByTagAsync(tag, TestContext.Current.CancellationToken);

        var afterInvalidation = await _cacheService.GetOrCreateWithMetadataAsync(
            key,
            Factory,
            policy,
            tags: [tag],
            ct: TestContext.Current.CancellationToken);

        Assert.Equal("SteamData1", initial.Value);
        Assert.Equal("SteamData2", afterInvalidation.Value);
        Assert.Equal(2, factoryCallCount);
        Assert.Equal(OwnedGamesCacheStatus.Miss, afterInvalidation.Cache.Status);
    }
}
