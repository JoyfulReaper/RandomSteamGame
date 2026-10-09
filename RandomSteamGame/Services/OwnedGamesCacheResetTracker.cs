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
    private readonly OwnedGamesRefreshAdmissionCoordinator _admission;

    public OwnedGamesCacheResetTracker(
        ICacheService cache,
        IDateTimeProvider dateTimeProvider,
        OwnedGamesRefreshAdmissionCoordinator admission)
    {
        _cache = cache;
        _dateTimeProvider = dateTimeProvider;
        _admission = admission;
    }

    public async Task<DateTimeOffset?> RefreshAsync(long steamId, Func<CancellationToken, Task> invalidate, CancellationToken ct = default)
    {
        using var admission = await _admission.AcquireAsync(steamId, ct);
        ct.ThrowIfCancellationRequested();
        var nextAvailableAt = await GetNextAvailableAtAsync(steamId, ct);
        if (nextAvailableAt is not null)
            return nextAvailableAt;

        ct.ThrowIfCancellationRequested();
        await invalidate(ct);
        ct.ThrowIfCancellationRequested();
        await MarkResetAsync(steamId, ct);
        return null;
    }

    private async Task<DateTimeOffset?> GetNextAvailableAtAsync(long steamId, CancellationToken ct)
    {
        DateTimeOffset? lastReset = await _cache.GetAsync<DateTimeOffset?>(GetCacheKey(steamId), ct);

        if (lastReset is null)
        {
            return null;
        }

        return lastReset.Value.Add(OwnedGamesCacheResetCooldown);
    }

    private async Task MarkResetAsync(long steamId, CancellationToken ct)
    {
        var now = new DateTimeOffset(_dateTimeProvider.UtcNow);
        await _cache.SetAsync(
            GetCacheKey(steamId),
            now,
            OwnedGamesCacheResetPolicy,
            ct: ct);
    }

    private static string GetCacheKey(long steamId)
        => $"owned_games_cache_reset_{steamId}";
}
