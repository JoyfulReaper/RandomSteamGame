/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Caching.Hybrid;

namespace SteamApiClient.Extensions;

internal static class HybridCacheExtensions
{
    public static async Task<T?> GetAsync<T>(this HybridCache cache, string key, CancellationToken ct = default)
    {
        var options = new HybridCacheEntryOptions
        {
            Flags = HybridCacheEntryFlags.DisableLocalCacheWrite |
                    HybridCacheEntryFlags.DisableDistributedCacheWrite
        };

        return await cache.GetOrCreateAsync<T>(key, _ => default!, options, cancellationToken: ct);
    }
}