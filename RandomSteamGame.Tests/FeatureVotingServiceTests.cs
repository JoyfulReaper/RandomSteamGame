using JoyfulReaperLib.Sqlite;
using Microsoft.Data.Sqlite;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;

namespace RandomSteamGame.Tests;

public sealed class FeatureVotingServiceTests : IDisposable
{
    private readonly VotingTestDatabase _db = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly string Browser = new('a', 64);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SchemaIsAdditiveIdempotentAndDoesNotSeedProposals(bool populated)
    {
        using var db = new VotingTestDatabase(initialize: false);
        db.Execute("""
            CREATE TABLE Visitors (IpAddress TEXT PRIMARY KEY, Hits INTEGER, LastSeen TEXT);
            CREATE TABLE AppStats (Id INTEGER PRIMARY KEY, RandomGamesGenerated INTEGER, LibrariesExported INTEGER);
            """ + SqliteRecentPickService.SchemaSql);
        if (populated)
            db.Execute("""
                INSERT INTO Visitors VALUES ('existing-visitor', 7, 'existing-time');
                INSERT INTO AppStats VALUES (1, 23, 4);
                INSERT INTO RecentPicks VALUES (400, 'Portal', 'existing-time');
                """);
        var before = db.Scalar("SELECT group_concat(sql) FROM sqlite_schema WHERE type = 'table';");
        SqliteFeatureVotingService.InitializeSchema(db.ConnectionString);
        SqliteFeatureVotingService.InitializeSchema(db.ConnectionString);
        Assert.Empty(await db.Service.GetAsync(Browser, Ct));
        Assert.Equal(populated ? 7L : 0L, db.Scalar("SELECT COALESCE(SUM(Hits), 0) FROM Visitors;"));
        Assert.Equal(populated ? 23L : 0L, db.Scalar("SELECT COALESCE(SUM(RandomGamesGenerated), 0) FROM AppStats;"));
        Assert.Equal(populated ? 4L : 0L, db.Scalar("SELECT COALESCE(SUM(LibrariesExported), 0) FROM AppStats;"));
        Assert.Equal(populated ? "Portal" : null, db.Scalar("SELECT GameName FROM RecentPicks;"));
        Assert.Equal(before, db.Scalar("SELECT group_concat(sql) FROM sqlite_schema WHERE type = 'table' AND name IN ('Visitors', 'AppStats', 'RecentPicks');"));
    }

    [Fact]
    public async Task VotesAreIdempotentPersistentAndIndependentAcrossProposalsAndBrowsers()
    {
        _db.AddProposal(1);
        _db.AddProposal(2);
        Assert.Equal(FeatureVoteResult.Saved, await _db.Service.SetSupportedAsync(1, Browser, true, Ct));
        var created = _db.Scalar("SELECT CreatedAtUtc FROM FeatureVotes;");
        await _db.Service.SetSupportedAsync(1, Browser, true, Ct);
        Assert.Equal(created, _db.Scalar("SELECT CreatedAtUtc FROM FeatureVotes;"));
        await _db.Service.SetSupportedAsync(2, Browser, true, Ct);
        await _db.Service.SetSupportedAsync(1, new('b', 64), true, Ct);
        var persisted = await new SqliteFeatureVotingService(_db.ConnectionString).GetAsync(Browser, Ct);
        Assert.Equal(new long[] { 2, 1 }, persisted.Select(p => p.VoteCount));
        Assert.All(persisted, p => Assert.True(p.Supported));
        Assert.False((await _db.Service.GetAsync(new('c', 64), Ct))[0].Supported);
        await _db.Service.SetSupportedAsync(1, Browser, false, Ct);
        await _db.Service.SetSupportedAsync(1, Browser, false, Ct);
        var after = await _db.Service.GetAsync(Browser, Ct);
        Assert.False(after[0].Supported);
        Assert.Equal(1, after[0].VoteCount);
        Assert.True(after[1].Supported);
        _db.Execute(SqliteFeatureVotingService.SchemaSql);
        Assert.Equal(2L, _db.Scalar("SELECT COUNT(*) FROM FeatureVotes;"));
        Assert.Equal(TimeSpan.Zero, DateTimeOffset.ParseExact((string)created!, "O", System.Globalization.CultureInfo.InvariantCulture).Offset);
    }

    [Fact]
    public async Task ConcurrentServicesCannotInsertDuplicates()
    {
        _db.AddProposal(1);
        var tasks = Enumerable.Range(0, 12).Select(_ => Task.Run(() =>
            new SqliteFeatureVotingService(_db.ConnectionString).SetSupportedAsync(1, Browser, true, Ct), Ct));
        Assert.All(await Task.WhenAll(tasks), result => Assert.Equal(FeatureVoteResult.Saved, result));
        Assert.Equal(1L, _db.Scalar("SELECT COUNT(*) FROM FeatureVotes;"));
    }

    [Theory]
    [InlineData("planned", true)]
    [InlineData("planned", false)]
    [InlineData("completed", true)]
    [InlineData("completed", false)]
    [InlineData("closed", true)]
    [InlineData("closed", false)]
    public async Task MissingAndNonOpenProposalsRejectMutations(string status, bool supported)
    {
        _db.AddProposal(1);
        await _db.Service.SetSupportedAsync(1, Browser, true, Ct);
        using (var connection = _db.Open())
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE FeatureProposals SET Status = $status;";
            command.Parameters.AddWithValue("$status", status);
            command.ExecuteNonQuery();
        }
        Assert.Equal(FeatureVoteResult.Closed, await _db.Service.SetSupportedAsync(1, Browser, supported, Ct));
        Assert.Equal(FeatureVoteResult.NotFound, await _db.Service.SetSupportedAsync(999, Browser, supported, Ct));
        var closed = Assert.Single(await _db.Service.GetAsync(Browser, Ct));
        Assert.Equal(status, closed.Status);
        Assert.True(closed.Supported);
        Assert.Equal(1, closed.VoteCount);
    }

    [Fact]
    public async Task ContentionHasNoPartialWritesAndRecovers()
    {
        _db.AddProposal(1);
        using (var connection = _db.Open())
        using (var transaction = connection.BeginTransaction(deferred: false))
        {
            var exception = await Assert.ThrowsAsync<SqliteException>(() => _db.Service.SetSupportedAsync(1, Browser, true, Ct));
            Assert.Contains(exception.SqliteErrorCode, new[] { 5, 6 });
        }
        Assert.Equal(0L, _db.Scalar("SELECT COUNT(*) FROM FeatureVotes;"));
        await _db.Service.SetSupportedAsync(1, Browser, true, Ct);
        Assert.Equal(1L, _db.Scalar("SELECT COUNT(*) FROM FeatureVotes;"));
    }

    [Fact]
    public void DatabaseConstraintsAndPrivacyAreEnforced()
    {
        _db.AddProposal(1);
        Assert.Throws<SqliteException>(() => _db.Execute("INSERT INTO FeatureProposals VALUES (2, 'feature-1', 'Duplicate', 'Description', 'open', NULL);"));
        Assert.Throws<SqliteException>(() => _db.Execute("INSERT INTO FeatureVotes VALUES (999, '" + Browser + "', 'timestamp');"));
        Assert.Throws<SqliteException>(() => _db.Execute("INSERT INTO FeatureVotes VALUES (1, 'raw-identifier', 'timestamp');"));
        _db.Execute("INSERT INTO FeatureVotes VALUES (1, '" + Browser + "', 'timestamp');");
        Assert.Throws<SqliteException>(() => _db.Execute("INSERT INTO FeatureVotes VALUES (1, '" + Browser + "', 'timestamp');"));
        Assert.Equal("FeatureProposalId,BrowserIdentityHash,CreatedAtUtc", _db.Scalar("SELECT group_concat(name) FROM pragma_table_info('FeatureVotes');"));
        Assert.Throws<SqliteException>(() => _db.AddProposal(3, "invalid"));
    }

    [Fact]
    public async Task LegacyStatusUpgradePreservesProposalsVotesAndSchemaObjectsAndIsIdempotent()
    {
        using var db = new VotingTestDatabase(initialize: false);
        db.Execute(LegacySchemaSql);
        db.AddProposal(10);
        db.AddProposal(30, "closed");
        await db.Service.SetSupportedAsync(10, Browser, true, Ct);
        db.Execute("""
            CREATE TABLE ExistingData (Value TEXT);
            INSERT INTO ExistingData VALUES ('keep');
            CREATE INDEX CustomProposalTitle ON FeatureProposals(Title);
            CREATE TRIGGER KeepProposalAudit AFTER UPDATE ON FeatureProposals
                BEGIN INSERT INTO ExistingData VALUES ('updated'); END;
            """);
        var vote = db.Scalar("SELECT FeatureProposalId || ':' || BrowserIdentityHash || ':' || CreatedAtUtc FROM FeatureVotes;");
        var original = await db.Service.GetAsync(Browser, Ct);
        var journal = db.Scalar("PRAGMA journal_mode;");
        SqliteFeatureVotingService.InitializeSchema(db.ConnectionString);
        SqliteFeatureVotingService.InitializeSchema(db.ConnectionString);
        Assert.Equal(original, await db.Service.GetAsync(Browser, Ct));
        Assert.Equal(vote, db.Scalar("SELECT FeatureProposalId || ':' || BrowserIdentityHash || ':' || CreatedAtUtc FROM FeatureVotes;"));
        Assert.Equal("keep", db.Scalar("SELECT group_concat(Value) FROM ExistingData;"));
        Assert.Equal(journal, db.Scalar("PRAGMA journal_mode;"));
        Assert.Equal(1L, db.Scalar("SELECT COUNT(*) FROM sqlite_schema WHERE name = 'CustomProposalTitle';"));
        db.Execute("UPDATE FeatureProposals SET Status = 'planned' WHERE Id = 10;");
        Assert.Equal("keep,updated", db.Scalar("SELECT group_concat(Value) FROM ExistingData;"));
        db.AddProposal(40, "completed");
        Assert.Equal("planned", (await db.Service.GetAsync(Browser, Ct))[0].Status);
        Assert.Equal(1, (await db.Service.GetAsync(Browser, Ct))[0].VoteCount);
        Assert.Equal(0L, db.Scalar("SELECT COUNT(*) FROM pragma_foreign_key_check;"));
        Assert.Throws<SqliteException>(() => db.Execute("DELETE FROM FeatureProposals WHERE Id = 10;"));
        Assert.Throws<SqliteException>(() => db.Execute("INSERT INTO FeatureVotes VALUES (999, '" + Browser + "', 'timestamp');"));
        Assert.Equal(0L, db.Scalar("SELECT COUNT(*) FROM sqlite_schema WHERE name = 'FeatureProposals_StatusUpgrade';"));
    }

    [Fact]
    public async Task FailedLegacyUpgradeRollsBackWithoutChangingAnyExistingData()
    {
        using var db = new VotingTestDatabase(initialize: false);
        db.Execute(LegacySchemaSql);
        db.AddProposal(10);
        await db.Service.SetSupportedAsync(10, Browser, true, Ct);
        var schema = db.Scalar("SELECT sql FROM sqlite_schema WHERE name = 'FeatureProposals';");
        // Simulate pre-existing inconsistent references inserted by a connection without FKs.
        db.Execute("PRAGMA foreign_keys=OFF; INSERT INTO FeatureVotes VALUES (999, '" + Browser + "', 'keep-this-row');");
        var rows = db.Scalar("SELECT group_concat(FeatureProposalId || ':' || BrowserIdentityHash || ':' || CreatedAtUtc) FROM FeatureVotes;");
        Assert.Throws<InvalidOperationException>(() => SqliteFeatureVotingService.InitializeSchema(db.ConnectionString));
        Assert.Equal(schema, db.Scalar("SELECT sql FROM sqlite_schema WHERE name = 'FeatureProposals';"));
        Assert.Equal(rows, db.Scalar("SELECT group_concat(FeatureProposalId || ':' || BrowserIdentityHash || ':' || CreatedAtUtc) FROM FeatureVotes;"));
        Assert.Equal(1L, db.Scalar("SELECT COUNT(*) FROM FeatureProposals WHERE Id = 10;"));
        Assert.Equal(0L, db.Scalar("SELECT COUNT(*) FROM sqlite_schema WHERE name = 'FeatureProposals_StatusUpgrade';"));
    }

    // Immutable reproduction of the original PR #47 schema, not the current schema constant.
    private const string LegacySchemaSql = """
        CREATE TABLE FeatureProposals (
            Id INTEGER PRIMARY KEY,
            Slug TEXT NOT NULL UNIQUE CHECK (
                length(Slug) BETWEEN 1 AND 80 AND Slug NOT GLOB '*[^a-z0-9-]*'
                AND Slug NOT LIKE '-%' AND Slug NOT LIKE '%-'),
            Title TEXT NOT NULL CHECK (length(trim(Title)) BETWEEN 1 AND 160),
            Description TEXT NOT NULL CHECK (length(trim(Description)) BETWEEN 1 AND 4000),
            Status TEXT NOT NULL CHECK (Status IN ('open', 'closed')),
            GitHubIssueNumber INTEGER CHECK (GitHubIssueNumber > 0)
        );
        CREATE TABLE FeatureVotes (
            FeatureProposalId INTEGER NOT NULL REFERENCES FeatureProposals(Id),
            BrowserIdentityHash TEXT NOT NULL CHECK (
                length(BrowserIdentityHash) = 64 AND BrowserIdentityHash NOT GLOB '*[^0-9a-f]*'),
            CreatedAtUtc TEXT NOT NULL,
            PRIMARY KEY (FeatureProposalId, BrowserIdentityHash)
        );
        """;

    [Fact]
    public void LegacyUpgradeDoesNotSilentlyDiscardUnexpectedColumns()
    {
        using var db = new VotingTestDatabase(initialize: false);
        db.Execute(LegacySchemaSql);
        db.AddProposal(10);
        db.Execute("ALTER TABLE FeatureProposals ADD COLUMN CustomData TEXT; UPDATE FeatureProposals SET CustomData = 'preserve';");
        var schema = db.Scalar("SELECT sql FROM sqlite_schema WHERE name = 'FeatureProposals';");
        Assert.Throws<SqliteException>(() => SqliteFeatureVotingService.InitializeSchema(db.ConnectionString));
        Assert.Equal(schema, db.Scalar("SELECT sql FROM sqlite_schema WHERE name = 'FeatureProposals';"));
        Assert.Equal("preserve", db.Scalar("SELECT CustomData FROM FeatureProposals WHERE Id = 10;"));
        Assert.Equal(0L, db.Scalar("SELECT COUNT(*) FROM sqlite_schema WHERE name = 'FeatureProposals_StatusUpgrade';"));
    }

    public void Dispose() => _db.Dispose();
}

internal sealed class VotingTestDatabase : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"feature-voting-{Guid.NewGuid():N}.db");
    public string ConnectionString => new SqliteConnectionStringBuilder { DataSource = Path, ForeignKeys = true, Pooling = false }.ToString();
    public SqliteFeatureVotingService Service => new(ConnectionString);

    public VotingTestDatabase(bool initialize = true)
    {
        SqliteProviderInitializer.Initialize();
        if (initialize) SqliteFeatureVotingService.InitializeSchema(ConnectionString);
    }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        return connection;
    }

    public void Execute(string sql)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public object? Scalar(string sql)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    public void AddProposal(long id, string status = "open")
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO FeatureProposals (Id, Slug, Title, Description, Status) VALUES ($id, $slug, 'Test proposal', 'Test description', $status);";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$slug", $"feature-{id}");
        command.Parameters.AddWithValue("$status", status);
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        File.Delete(Path);
        File.Delete(Path + "-wal");
        File.Delete(Path + "-shm");
    }
}
