/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using SteamApiClient.Settings;

namespace SteamApiClient.Services;

public class CacheService : ICacheService
{
    private readonly HybridCache _cache;
    private readonly ILogger<CacheService> _logger;

    public CacheService(
        HybridCache cache,
        ILogger<CacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CachePolicy policy,
        CancellationToken ct = default)
    {
        var options = new HybridCacheEntryOptions
        {
            // set expiration lifetimes for RAM (L1) and DB (L2)
            Expiration = policy.Duration,
            LocalCacheExpiration = policy.Duration
        };

        _logger.LogDebug("HybridCache executing lookups for key: {Key}", key);

        // handles checking L1, fallback to L2 DB, and Single-Flight token locking
        return await _cache.GetOrCreateAsync(
            key,
            async token => await factory(token),
            options,
            cancellationToken: ct);
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        CachePolicy policy,
        CancellationToken ct = default)
    {
        if (value is null) return;

        var options = new HybridCacheEntryOptions
        {
            Expiration = policy.Duration,
            LocalCacheExpiration = policy.Duration
        };

        await _cache.SetAsync(key, value, options, cancellationToken: ct);
        _logger.LogDebug("HybridCache direct write: {Key}", key);
    }
}