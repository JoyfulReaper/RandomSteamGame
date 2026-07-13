/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using SteamApiClient.Settings;

namespace SteamApiClient.Services;

public interface ICacheService
{
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CachePolicy policy,
        IEnumerable<string>? tags = null,
        CancellationToken ct = default);

    Task SetAsync<T>(
        string key,
        T value,
        CachePolicy policy,
        IEnumerable<string>? tags = null,
        CancellationToken ct = default);

    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    Task<CacheLookupResult<T>> GetOrCreateWithMetadataAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CachePolicy policy,
        IEnumerable<string>? tags = null,
        CancellationToken ct = default);

    Task InvalidateByTagAsync(string tag, CancellationToken ct = default);
}

public sealed record CacheLookupResult<T>(
    T Value,
    OwnedGamesCacheInfo Cache);
