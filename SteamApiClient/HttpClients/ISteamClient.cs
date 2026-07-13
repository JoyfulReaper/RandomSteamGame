/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */


using SteamApiClient.Contracts.SteamApi;

namespace SteamApiClient.HttpClients;

public interface ISteamClient
{
    Task<OwnedGames> GetOwnedGames(
        long steamId,
        bool includeAppInfo = true,
        bool includePlayedFreeGames = true,
        CancellationToken ct = default);

    Task<OwnedGamesResult> GetOwnedGamesWithCacheInfo(
        long steamId,
        bool includeAppInfo = true,
        bool includePlayedFreeGames = true,
        CancellationToken ct = default);

    Task<long> GetSteamIdFromVanityUrl(
        string vanityUrl,
        CancellationToken ct = default);

    Task InvalidateOwnedGamesCacheAsync(long steamId);
}
