/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Client.Services.Interfaces;

public interface IGameApiClient
{
    Task<OwnedGamesResponse?> GetOwnedGamesAsync(long steamId);
    Task<RandomGameResponse?> GetRandomSteamGameAsync(long steamId);
    Task<GameDetails?> GetRandomGameBySteamIdAsync(long steamId);
    Task<GameDetails?> GetRandomGameByVanityUrlAsync(string vanityUrl);
    Task<long?> ResolveVanityUrlAsync(string vanityUrl);
}