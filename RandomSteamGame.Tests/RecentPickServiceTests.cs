using JoyfulReaperLib.Sqlite;
using Microsoft.Data.Sqlite;
using RandomSteamGame.Services;

namespace RandomSteamGame.Tests;

public sealed class RecentPickServiceTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"recent-picks-{Guid.NewGuid():N}.db");
    private readonly string _connectionString;
    private readonly SqliteRecentPickService _service;
    private static readonly DateTimeOffset PickedAt = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    public RecentPickServiceTests()
    {
        SqliteProviderInitializer.Initialize();
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = SqliteRecentPickService.SchemaSql;
        command.ExecuteNonQuery();
        _service = new SqliteRecentPickService(_connectionString);
    }

    [Fact]
    public async Task RecordAndRetrieve_PersistNewestFirstWithUtcTimestampsAndSafeNames()
    {
        await _service.RecordAsync(new(10, "  Portal\r\n\u001b[31m2\u001b[0m\u202e ", PickedAt.AddMinutes(1).ToOffset(TimeSpan.FromHours(3))), TestContext.Current.CancellationToken);
        await _service.RecordAsync(new(20, "Older", PickedAt), TestContext.Current.CancellationToken);
        // A new service and connection must read the persisted history.
        var picks = await new SqliteRecentPickService(_connectionString).GetRecentAsync(5, TestContext.Current.CancellationToken);
        Assert.Equal(new[] { 10, 20 }, picks.Select(pick => pick.AppId));
        Assert.Equal("Portal 2", picks[0].GameName);
        Assert.Equal(PickedAt.AddMinutes(1), picks[0].PickedAtUtc);
        Assert.Equal(TimeSpan.Zero, picks[0].PickedAtUtc.Offset);
    }

    [Fact]
    public async Task Record_PrunesToTwentyNewestTimestampsIncludingOutOfOrderWrites()
    {
        for (var index = 30; index >= 1; index--)
        {
            await _service.RecordAsync(new(index, $"Game {index}", PickedAt.AddSeconds(index)), TestContext.Current.CancellationToken);
        }

        var picks = await _service.GetRecentAsync(100, TestContext.Current.CancellationToken);
        Assert.Equal(Enumerable.Range(11, 20).Reverse(), picks.Select(pick => pick.AppId));
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM RecentPicks;";
        Assert.Equal(20L, command.ExecuteScalar());
    }

    [Fact]
    public async Task Record_RepeatedPicksAndTiedTimestampsRemainDistinctAndDeterministic()
    {
        for (var index = 1; index <= 25; index++)
        {
            await _service.RecordAsync(new(10, $"Pick {index}", PickedAt), TestContext.Current.CancellationToken);
        }

        var picks = await _service.GetRecentAsync(100, TestContext.Current.CancellationToken);
        Assert.Equal(Enumerable.Range(6, 20).Reverse().Select(index => $"Pick {index}"), picks.Select(pick => pick.GameName));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(20, 8)]
    public async Task GetRecent_ReturnsOnlyRequestedCount(int count, int expected)
    {
        for (var index = 1; index <= 8; index++)
        {
            await _service.RecordAsync(new(index, "Game", PickedAt.AddSeconds(index)), TestContext.Current.CancellationToken);
        }

        var picks = await _service.GetRecentAsync(count, TestContext.Current.CancellationToken);
        Assert.Equal(Enumerable.Range(1, 8).Reverse().Take(expected), picks.Select(pick => pick.AppId));
    }

    [Fact]
    public async Task GetRecent_EmptyHistoryAndNegativeCount()
    {
        Assert.Empty(await _service.GetRecentAsync(5, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.GetRecentAsync(-1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Record_SanitizesEmptyAndLongNames()
    {
        await _service.RecordAsync(new(1, "\u0000\u202e", PickedAt), TestContext.Current.CancellationToken);
        await _service.RecordAsync(new(2, new string('a', 300), PickedAt), TestContext.Current.CancellationToken);
        var picks = await _service.GetRecentAsync(5, TestContext.Current.CancellationToken);
        Assert.Equal(new string('a', 256), picks[0].GameName);
        Assert.Equal("Unknown game", picks[1].GameName);
    }

    [Fact]
    public async Task ConcurrentCalls_AcrossSameAndSeparateServicesKeepNewestTwenty()
    {
        var otherService = new SqliteRecentPickService(_connectionString);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(worker => Task.Run(async () =>
        {
            var service = worker % 2 == 0 ? _service : otherService;
            for (var index = worker * 15 + 1; index <= (worker + 1) * 15; index++)
            {
                await service.RecordAsync(new(index, $"Game {index}", PickedAt.AddSeconds(index)), TestContext.Current.CancellationToken);
                Assert.InRange((await service.GetRecentAsync(100, TestContext.Current.CancellationToken)).Count, 1, 20);
            }
        }, TestContext.Current.CancellationToken)));

        Assert.Equal(Enumerable.Range(41, 20).Reverse(),
            (await _service.GetRecentAsync(100, TestContext.Current.CancellationToken)).Select(pick => pick.AppId));
    }

    [Fact]
    public async Task PruningFailure_RollsBackInsert()
    {
        for (var index = 1; index <= 20; index++)
        {
            await _service.RecordAsync(new(index, "Game", PickedAt), TestContext.Current.CancellationToken);
        }
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TRIGGER FailPruning BEFORE DELETE ON RecentPicks
            BEGIN SELECT RAISE(ABORT, 'Test pruning failure'); END;
            """;
        command.ExecuteNonQuery();

        await Assert.ThrowsAsync<SqliteException>(() => _service.RecordAsync(new(21, "New", PickedAt), TestContext.Current.CancellationToken));
        var picks = await _service.GetRecentAsync(100, TestContext.Current.CancellationToken);
        Assert.Equal(20, picks.Count);
        Assert.DoesNotContain(picks, pick => pick.AppId == 21);
    }

    [Fact]
    public async Task BusyDatabase_DoesNotInsertPartialHistoryAndRecoversAfterLockReleased()
    {
        using (var connection = OpenConnection())
        using (var transaction = connection.BeginTransaction(deferred: false))
        {
            var failure = await Assert.ThrowsAsync<SqliteException>(() =>
                _service.RecordAsync(new(10, "Portal", PickedAt), TestContext.Current.CancellationToken));
            Assert.Contains(failure.SqliteErrorCode, new[] { 5, 6 });
        }

        Assert.Empty(await _service.GetRecentAsync(5, TestContext.Current.CancellationToken));
        await _service.RecordAsync(new(20, "Portal 2", PickedAt), TestContext.Current.CancellationToken);
        Assert.Equal(20, Assert.Single(await _service.GetRecentAsync(5, TestContext.Current.CancellationToken)).AppId);
    }

    [Fact]
    public void ModelAndSchema_ContainOnlyAnonymousDisplayFields()
    {
        var expected = new[] { "AppId", "GameName", "PickedAtUtc" };
        Assert.Equal(expected, typeof(RecentPick).GetProperties().Select(property => property.Name).Order());
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(RecentPicks);";
        using var reader = command.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }
        Assert.Equal(expected, columns.Order());
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public void Dispose() => File.Delete(_databasePath);
}
