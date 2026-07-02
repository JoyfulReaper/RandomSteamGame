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
}
