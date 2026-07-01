/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace RandomSteamGame.Shared.Contracts;

public sealed record AppStatsResponse(
    long TotalHits,
    long UniqueVisitors,
    long RandomGamesGenerated);
