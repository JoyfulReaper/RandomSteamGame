/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using SteamApiClient.Extensions;
using SteamApiClient.Settings;

namespace SteamApiClient.Services;

internal class CacheService : ICacheService
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
            LocalCacheExpiration = TimeSpan.FromMinutes(5) // TODO: Make configurable through appsettings
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
            LocalCacheExpiration = TimeSpan.FromMinutes(5) // TODO: Make configurable through appsettings
        };

        await _cache.SetAsync(key, value, options, tags, cancellationToken: ct);
        // _logger.LogDebug("HybridCache direct write: {Key}", key);
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        => _cache.GetAsync<T>(key, ct);

    public async Task<CacheLookupResult<T>> GetOrCreateWithMetadataAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CachePolicy policy,
        IEnumerable<string>? tags = null,
        CancellationToken ct = default)
    {
        var cached = await GetAsync<CachedValue<T>>(key, ct);
        if (cached is not null)
        {
            var ageSeconds = Math.Max(
                0,
                (long)(DateTimeOffset.UtcNow - cached.CachedAt).TotalSeconds);

            return new CacheLookupResult<T>(
                cached.Value,
                new OwnedGamesCacheInfo(OwnedGamesCacheStatus.Hit, ageSeconds));
        }

        var value = await factory(ct);
        var envelope = new CachedValue<T>(value, DateTimeOffset.UtcNow);
        await SetAsync(key, envelope, policy, tags, ct);

        return new CacheLookupResult<T>(
            value,
            new OwnedGamesCacheInfo(OwnedGamesCacheStatus.Miss, 0));
    }

    public async Task InvalidateByTagAsync(string tag, CancellationToken ct = default)
    {
        //_logger.LogDebug("Invalidating cache for tag: {Tag}", tag);
        await _cache.RemoveByTagAsync(tag, cancellationToken: ct);
    }
}
