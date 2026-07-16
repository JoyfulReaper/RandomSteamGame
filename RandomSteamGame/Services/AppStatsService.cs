/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.MissionControl;
using JoyfulReaperLib.WebStats.Sqlite;
using Microsoft.Data.Sqlite;
using RandomSteamGame.Events;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using System.Diagnostics;

namespace RandomSteamGame.Services;

public sealed class AppStatsService : IAppStatsService
{
    private readonly SqliteConnection _dbConnection;
    private readonly IHitCounter _hitCounter;
    private readonly IMissionControlClient _missionControlClient;
    private readonly ILogger<AppStatsService> _logger;

    public AppStatsService(
        SqliteConnection dbConnection,
        IHitCounter hitCounter,
        IMissionControlClient missionControlClient,
        ILogger<AppStatsService> logger)
    {
        _dbConnection = dbConnection;
        _hitCounter = hitCounter;
        _missionControlClient = missionControlClient;
        _logger = logger;
    }

    public async Task<AppStatsResponse> RecordHitAsync(string ip)
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var correlationId = Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();
        var stats = await _hitCounter.RecordHitAsync(ip);
        var randomGamesGenerated = await GetRandomGamesGeneratedAsync();
        var response = new AppStatsResponse(stats.TotalHits, stats.UniqueVisitors, randomGamesGenerated);

        try
        {
            await _missionControlClient.TryPublishAsync(
                eventType: RandomSteamGameEventTypes.SiteVisitRecorded,
                payload: new SiteVisitRecordedEvent(
                    TotalHits: response.TotalHits,
                    UniqueVisitors: response.UniqueVisitors,
                    DurationMilliseconds: stopwatch.ElapsedMilliseconds),
                occurredAt: occurredAt,
                correlationId: correlationId,
                cancellationToken: CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to publish site visit event {CorrelationId}.",
                correlationId);
        }

        return response;
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
