/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.Sqlite;
using JoyfulReaperLib.WebStats.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using RandomSteamGame.Services;

namespace RandomSteamGame.Tests;

public class AppStatsServiceTests : IDisposable
{
    private readonly string _basePath;
    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly AppStatsService _service;
    private readonly SqliteConnection _connection;

    public AppStatsServiceTests()
    {
        SqliteProviderInitializer.Initialize();

        _basePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_basePath);

        _databasePath = Path.Combine(_basePath, "app-stats.db");
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();

        InitializeSchema();

        _connection = new SqliteConnection(_connectionString);
        var hitCounter = new SqliteHitCounter(Microsoft.Extensions.Options.Options.Create(new SqliteHitCounterOptions
        {
            ConnectionString = _connectionString
        }));
        _service = new AppStatsService(_connection, hitCounter);
    }

    [Fact]
    public async Task GetStatsAsync_EmptyVisitorsTable_ReturnsZeroHitCounts()
    {
        var stats = await _service.GetStatsAsync();

        Assert.Equal(0, stats.TotalHits);
        Assert.Equal(0, stats.UniqueVisitors);
        Assert.Equal(0, stats.RandomGamesGenerated);
    }

    [Fact]
    public async Task RecordHitAsync_OneVisitor_ReturnsOneHitAndOneUniqueVisitor()
    {
        var stats = await _service.RecordHitAsync("visitor-a");

        Assert.Equal(1, stats.TotalHits);
        Assert.Equal(1, stats.UniqueVisitors);
    }

    [Fact]
    public async Task RecordHitAsync_SameVisitorTwice_IncrementsHitsButNotUniqueVisitors()
    {
        await _service.RecordHitAsync("visitor-a");
        var stats = await _service.RecordHitAsync("visitor-a");

        Assert.Equal(2, stats.TotalHits);
        Assert.Equal(1, stats.UniqueVisitors);
    }

    [Fact]
    public async Task RecordHitAsync_TwoVisitors_ReturnsTwoUniqueVisitors()
    {
        await _service.RecordHitAsync("visitor-a");
        var stats = await _service.RecordHitAsync("visitor-b");

        Assert.Equal(2, stats.TotalHits);
        Assert.Equal(2, stats.UniqueVisitors);
    }

    [Fact]
    public async Task IncrementRandomGamesGeneratedAsync_IncrementsStoredCounter()
    {
        await _service.IncrementRandomGamesGeneratedAsync();
        await _service.IncrementRandomGamesGeneratedAsync();

        var stats = await _service.GetStatsAsync();

        Assert.Equal(2, stats.RandomGamesGenerated);
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearAllPools();

        DeleteIfExists(_databasePath);
        DeleteIfExists(_databasePath + "-wal");
        DeleteIfExists(_databasePath + "-shm");

        if (Directory.Exists(_basePath))
        {
            Directory.Delete(_basePath, recursive: true);
        }
    }

    private void InitializeSchema()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Visitors (
                IpAddress TEXT PRIMARY KEY,
                Hits INTEGER NOT NULL DEFAULT 1,
                LastSeen TEXT
            );

            CREATE TABLE IF NOT EXISTS AppStats (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                RandomGamesGenerated INTEGER NOT NULL DEFAULT 0
            );

            INSERT INTO AppStats (Id, RandomGamesGenerated)
            SELECT 1, 0
            WHERE NOT EXISTS (SELECT 1 FROM AppStats WHERE Id = 1);
            """;
        command.ExecuteNonQuery();
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
