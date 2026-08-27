/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Services.Interfaces;

public interface IAppStatsService
{
    Task<AppStatsResponse> RecordHitAsync(
        string ip,
        string? userAgent = null);

    Task<AppStatsResponse> GetStatsAsync();

    Task IncrementRandomGamesGeneratedAsync();
}
