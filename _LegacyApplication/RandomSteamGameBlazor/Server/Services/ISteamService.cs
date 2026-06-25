/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using ErrorOr;
using RandomSteamGameBlazor.Shared.Contracts.RandomSteamGame;
using RandomSteamGameBlazor.Shared.Contracts.Steam;

namespace RandomSteamGameBlazor.Server.Services;

public interface ISteamService
{
    Task<ErrorOr<OwnedGamesResponse>> GetOwnedGamesAsync(long steamId);
    Task<ErrorOr<RandomGameResponse>> GetRandomSteamGameAsync(long steamId);
    Task<ErrorOr<AppData>> GetRandomGameBySteamIdAsync(long steamId);
    Task<ErrorOr<long>> ResolveVanityUrlAsync(string vanityUrl);
}