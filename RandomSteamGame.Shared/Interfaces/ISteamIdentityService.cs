/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */
namespace RandomSteamGame.Shared.Interfaces;

public interface ISteamIdentityService
{
    Task<long?> GetSteamIdAsync();
    Task SetIdentityAsync(long steamId, string? vanityUrl);
    Task<string?> GetVanityUrlAsync();
    Task LogoutAsync();
}