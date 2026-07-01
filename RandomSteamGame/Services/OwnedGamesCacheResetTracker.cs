/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Interfaces;
using SteamApiClient.Services;
using SteamApiClient.Settings;

namespace RandomSteamGame.Services;

public sealed class OwnedGamesCacheResetTracker : IOwnedGamesCacheResetTracker
{
    private static readonly TimeSpan OwnedGamesCacheResetCooldown = TimeSpan.FromHours(12);
    private static readonly CachePolicy OwnedGamesCacheResetPolicy = new()
    {
        AbsoluteMinutes = 12 * 60
    };

    private readonly ICacheService _cache;
    private readonly IDateTimeProvider _dateTimeProvider;

    public OwnedGamesCacheResetTracker(
        ICacheService cache,
        IDateTimeProvider dateTimeProvider)
    {
        _cache = cache;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<DateTimeOffset?> GetNextAvailableAtAsync(long steamId)
    {
        DateTimeOffset? lastReset = await _cache.GetAsync<DateTimeOffset?>(GetCacheKey(steamId));

        if (lastReset is null)
        {
            return null;
        }

        return lastReset.Value.Add(OwnedGamesCacheResetCooldown);
    }

    public async Task MarkResetAsync(long steamId)
    {
        var now = new DateTimeOffset(_dateTimeProvider.UtcNow);
        await _cache.SetAsync(
            GetCacheKey(steamId),
            now,
            OwnedGamesCacheResetPolicy);
    }

    private static string GetCacheKey(long steamId)
        => $"owned_games_cache_reset_{steamId}";
}
