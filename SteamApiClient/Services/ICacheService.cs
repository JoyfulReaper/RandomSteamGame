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
    // Shares only in-flight work; the factory decides which results to persist and their policies.
    Task<T> CoalesceAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default);

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

    // T? is an annotation for unconstrained T: use nullable value types for null-on-miss,
    // and use exactly the same T for SetAsync and GetAsync on a key.
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
