/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace RandomSteamGame.Services.Interfaces;

public interface IOwnedGamesCacheResetTracker
{
    // Runs invalidation under per-ID admission and commits cooldown only on success.
    // Returns the persisted next-available time when rejected, or null on success.
    Task<DateTimeOffset?> RefreshAsync(long steamId, Func<CancellationToken, Task> invalidate, CancellationToken ct = default);
}
