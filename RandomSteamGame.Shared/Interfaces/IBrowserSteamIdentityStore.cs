/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Shared.Interfaces;

public interface IBrowserSteamIdentityStore
{
    ValueTask<SteamIdentity> GetIdentityAsync();
    ValueTask SetIdentityAsync(SteamIdentity identity);
    ValueTask ClearAsync();
    ValueTask<DateTimeOffset?> GetOwnedGamesCacheResetAtAsync();
    ValueTask SetOwnedGamesCacheResetAtAsync(DateTimeOffset resetAt);
    ValueTask ClearOwnedGamesCacheResetAtAsync();
}
