/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace RandomSteamGame.Services.Interfaces;

public interface IOwnedGamesCacheResetTracker
{
    Task<DateTimeOffset?> GetNextAvailableAtAsync(long steamId);
    Task MarkResetAsync(long steamId);
}
