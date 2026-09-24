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
    private readonly IVisitorIdProvider _visitorIdProvider;
    private const int MaxUserAgentLength = 512;

    public AppStatsService(
        SqliteConnection dbConnection,
        IHitCounter hitCounter,
        IMissionControlClient missionControlClient,
        IVisitorIdProvider visitorIdProvider,
        ILogger<AppStatsService> logger)
    {
        _dbConnection = dbConnection;
        _hitCounter = hitCounter;
        _missionControlClient = missionControlClient;
        _visitorIdProvider = visitorIdProvider;
        _logger = logger;
    }

    public async Task<AppStatsResponse> RecordHitAsync(
        string ip,
        string? userAgent = null)
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var correlationId = Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();
        var stats = await _hitCounter.RecordHitAsync(ip);
        var counters = await GetApplicationCountersAsync();
        var response =
            new AppStatsResponse(
                stats.TotalHits,
                stats.UniqueVisitors,
                counters.RandomGamesGenerated,
                counters.LibrariesExported);

        try
        {
            var visitorId = _visitorIdProvider.GetVisitorId(ip);
            var isUniqueVisitor = await IsUniqueVisitorHitAsync(ip);
            var normalizedUserAgent = NormalizeUserAgent(userAgent);

            await _missionControlClient.TryPublishAsync(
                eventType: RandomSteamGameEventTypes.SiteVisitRecorded,
                payload: new SiteVisitRecordedEvent(
                    VisitorId: visitorId,
                    UserAgent: normalizedUserAgent,
                    IsUniqueVisitor: isUniqueVisitor,
                    TotalHits: response.TotalHits,
                    UniqueVisitors: response.UniqueVisitors,
                    DurationMilliseconds: stopwatch.ElapsedMilliseconds),
                payloadTypeInfo: RandomSteamGameJsonContext.Default.SiteVisitRecordedEvent,
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
        var counters = await GetApplicationCountersAsync();

        return new AppStatsResponse(
            stats.TotalHits,
            stats.UniqueVisitors,
            counters.RandomGamesGenerated,
            counters.LibrariesExported);
    }

    public async Task IncrementLibrariesExportedAsync()
    {
        if (_dbConnection.State !=
            System.Data.ConnectionState.Open)
        {
            await _dbConnection.OpenAsync();
        }

        await using var command = _dbConnection.CreateCommand();

        command.CommandText = """
            UPDATE AppStats
            SET LibrariesExported =
                LibrariesExported + 1
            WHERE Id = 1;
            """;

        await command.ExecuteNonQueryAsync();
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

    private async Task<(long RandomGamesGenerated, long LibrariesExported)>
        GetApplicationCountersAsync()
    {
        if (_dbConnection.State !=
            System.Data.ConnectionState.Open)
        {
            await _dbConnection.OpenAsync();
        }

        await using var command = _dbConnection.CreateCommand();
        command.CommandText = """
            SELECT
                RandomGamesGenerated,
                LibrariesExported
            FROM AppStats
            WHERE Id = 1;
            """;

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return (0, 0);
        }

        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    private async Task<bool> IsUniqueVisitorHitAsync(string visitorKey)
    {
        if (_dbConnection.State != System.Data.ConnectionState.Open)
        {
            await _dbConnection.OpenAsync();
        }

        await using var command = _dbConnection.CreateCommand();
        command.CommandText = """
        SELECT Hits
        FROM Visitors
        WHERE IpAddress = $visitorKey
        LIMIT 1;
        """;

        command.Parameters.AddWithValue("$visitorKey", visitorKey.Trim());

        var result = await command.ExecuteScalarAsync();

        return result is not null
            && result is not DBNull
            && Convert.ToInt64(result) == 1;
    }

    private static string? NormalizeUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        var trimmed = userAgent.Trim();

        return trimmed.Length <= MaxUserAgentLength
            ? trimmed
            : trimmed[..MaxUserAgentLength];
    }
}
