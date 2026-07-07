/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.WebStats.Sqlite;
using Microsoft.Data.Sqlite;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Services;

public sealed class AppStatsService : IAppStatsService
{
    private readonly SqliteConnection _dbConnection;
    private readonly IHitCounter _hitCounter;

    public AppStatsService(SqliteConnection dbConnection, IHitCounter hitCounter)
    {
        _dbConnection = dbConnection;
        _hitCounter = hitCounter;
    }

    public async Task<AppStatsResponse> RecordHitAsync(string ip)
    {
        var stats = await _hitCounter.RecordHitAsync(ip);
        var randomGamesGenerated = await GetRandomGamesGeneratedAsync();
        return new AppStatsResponse(stats.TotalHits, stats.UniqueVisitors, randomGamesGenerated);
    }

    public async Task<AppStatsResponse> GetStatsAsync()
    {
        var stats = await _hitCounter.GetHitCountsAsync();
        var randomGamesGenerated = await GetRandomGamesGeneratedAsync();
        return new AppStatsResponse(stats.TotalHits, stats.UniqueVisitors, randomGamesGenerated);
    }

    public async Task IncrementRandomGamesGeneratedAsync()
    {
        if (_dbConnection.State != System.Data.ConnectionState.Open)
        {
            await _dbConnection.OpenAsync();
        }

        await using var command = _dbConnection.CreateCommand();
        command.CommandText = """
            INSERT INTO AppStats (Id, RandomGamesGenerated)
            VALUES (1, 1)
            ON CONFLICT(Id) DO UPDATE SET RandomGamesGenerated = RandomGamesGenerated + 1;
            """;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<long> GetRandomGamesGeneratedAsync()
    {
        if (_dbConnection.State != System.Data.ConnectionState.Open)
        {
            await _dbConnection.OpenAsync();
        }

        await using var command = _dbConnection.CreateCommand();
        command.CommandText = "SELECT RandomGamesGenerated FROM AppStats WHERE Id = 1;";

        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? 0 : Convert.ToInt64(result);
    }
}
