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
    internal static readonly TimeSpan LocalCacheExpiration = TimeSpan.FromMinutes(5);

    public static async Task<T?> GetAsync<T>(this HybridCache cache, string key, CancellationToken ct = default)
    {
        var options = new HybridCacheEntryOptions
        {
            // A miss must not invoke the factory or store a synthetic default; L2 hits may warm L1.
            Flags = HybridCacheEntryFlags.DisableUnderlyingData |
                    HybridCacheEntryFlags.DisableDistributedCacheWrite,
            LocalCacheExpiration = LocalCacheExpiration
        };

        return await cache.GetOrCreateAsync<T>(key, _ => default!, options, cancellationToken: ct);
    }
}
