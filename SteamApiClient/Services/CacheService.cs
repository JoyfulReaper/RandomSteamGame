/*
 * Steam Api Client
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
            IEnumerable<string>? tags = null,
            CancellationToken ct = default)
    {
        var options = new HybridCacheEntryOptions
        {
            Expiration = policy.Duration,
            LocalCacheExpiration = TimeSpan.FromMinutes(5)
        };

        //_logger.LogDebug("HybridCache executing lookups for key: {Key}", key);

        return await _cache.GetOrCreateAsync(
            key,
            async token => await factory(token),
            options,
            tags,
            cancellationToken: ct);
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        CachePolicy policy,
        IEnumerable<string>? tags = null,
        CancellationToken ct = default)
    {
        if (value is null)
            return;

        var options = new HybridCacheEntryOptions
        {
            Expiration = policy.Duration,
            LocalCacheExpiration = TimeSpan.FromMinutes(5)
        };

        await _cache.SetAsync(key, value, options, tags, cancellationToken: ct);
        // _logger.LogDebug("HybridCache direct write: {Key}", key);
    }

    public async Task InvalidateByTagAsync(string tag, CancellationToken ct = default)
    {
        _logger.LogDebug("Invalidating cache for tag: {Tag}", tag);
        await _cache.RemoveByTagAsync(tag, cancellationToken: ct);
    }
}