/*
 * Random Steam Game
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
        CancellationToken ct = default);

    Task SetAsync<T>(
        string key,
        T value,
        CachePolicy policy,
        CancellationToken ct = default);
}