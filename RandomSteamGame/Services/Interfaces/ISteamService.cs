/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using ErrorOr;
using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Services.Interfaces;

public interface ISteamService
{
    Task<ErrorOr<OwnedGamesResponse>> GetOwnedGamesAsync(long steamId);
    Task<ErrorOr<RandomGameResponse>> GetRandomSteamGameAsync(long steamId);
    Task<ErrorOr<GameDetails>> GetRandomGameBySteamIdAsync(long steamId);
    Task<ErrorOr<long>> ResolveVanityUrlAsync(string vanityUrl);
}