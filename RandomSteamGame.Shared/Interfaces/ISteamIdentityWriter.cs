/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Shared.Interfaces;

public interface ISteamIdentityWriter
{
    Task SetIdentityAsync(SteamIdentity identity);
    Task ClearAsync();
}