using JoyfulReaperLib.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RandomSteamGame.Extensions;
using RandomSteamGame.Options;
using RandomSteamGame.Services;
using System.Security.AccessControl;
using System.Security.Principal;

namespace RandomSteamGame.Tests;

public class LocalReadinessHealthCheckTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly List<ServiceProvider> _providers = [];
    public static bool IsWindows => OperatingSystem.IsWindows();
    private string DatabasePath => Path.Combine(_tempDirectory, "ready.db");
    internal const string Schema = """
        CREATE TABLE Visitors (IpAddress TEXT PRIMARY KEY, Hits INTEGER NOT NULL DEFAULT 1, LastSeen TEXT);
        CREATE TABLE AppStats (Id INTEGER PRIMARY KEY CHECK (Id = 1), RandomGamesGenerated INTEGER NOT NULL DEFAULT 0, LibrariesExported INTEGER NOT NULL DEFAULT 0);
        INSERT INTO AppStats VALUES (1, 17, 23);
        INSERT INTO Visitors VALUES ('test-visitor', 5, '2026-10-09');
        """;

    public LocalReadinessHealthCheckTests()
    {
        SqliteProviderInitializer.Initialize();

        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task CheckHealthAsync_Succeeds_WithWritableKeysAndValidSqlite()
    {
        await ExecuteSqlAsync(Schema);
        var check = CreateCheck(_tempDirectory, DatabasePath);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Empty(Directory.GetFiles(_tempDirectory, ".readiness-*.tmp"));
    }

    [Fact]
    public async Task CheckHealthAsync_Fails_ForBlankDatabase()
    {
        var result = await CheckAsync();
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Required SQLite schema is unavailable.", result.Description);
    }

    [Theory]
    [InlineData("DROP TABLE Visitors;")]
    [InlineData("DROP TABLE AppStats;")]
    [InlineData("ALTER TABLE Visitors DROP COLUMN Hits;")]
    [InlineData("ALTER TABLE Visitors DROP COLUMN LastSeen;")]
    [InlineData("ALTER TABLE Visitors RENAME COLUMN IpAddress TO Missing;")]
    [InlineData("ALTER TABLE AppStats DROP COLUMN RandomGamesGenerated;")]
    [InlineData("ALTER TABLE AppStats DROP COLUMN LibrariesExported;")]
    [InlineData("ALTER TABLE AppStats RENAME COLUMN Id TO Missing;")]
    public async Task CheckHealthAsync_Fails_ForMissingSchema(string damage)
    {
        await ExecuteSqlAsync(Schema + damage);
        var result = await CheckAsync();
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Required SQLite schema is unavailable.", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_Fails_ForMissingBootstrapRow()
    {
        await ExecuteSqlAsync(Schema + "DELETE FROM AppStats;");
        var result = await CheckAsync();
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Required SQLite bootstrap row is unavailable.", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_Fails_ForReadOnlyDatabase()
    {
        await ExecuteSqlAsync(Schema);
        var check = CreateCheck(_tempDirectory, DatabasePath, SqliteOpenMode.ReadOnly);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("SQLite database is not writable.", result.Description);
        Assert.IsType<SqliteException>(result.Exception);
        Assert.DoesNotContain(DatabasePath, result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_RollsBackWriteProbe()
    {
        await ExecuteSqlAsync(Schema);
        // A trigger makes a surviving write observable even though the probe assigns the same value.
        await ExecuteSqlAsync("""
            CREATE TRIGGER probe_write AFTER UPDATE ON AppStats
            BEGIN
                UPDATE AppStats SET LibrariesExported = LibrariesExported + 1 WHERE Id = 1;
            END;
            """);
        Assert.Equal(HealthStatus.Healthy, (await CheckAsync()).Status);
        await using var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT RandomGamesGenerated, LibrariesExported FROM AppStats WHERE Id = 1;";
        await using (var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
            Assert.Equal(17, reader.GetInt64(0));
            Assert.Equal(23, reader.GetInt64(1));
        }
        command.CommandText = "SELECT Hits FROM Visitors WHERE IpAddress = 'test-visitor';";
        Assert.Equal(5L, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CheckHealthAsync_Fails_WhenAnotherWriterHoldsDatabase()
    {
        await ExecuteSqlAsync(Schema);
        await using var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        using var transaction = connection.BeginTransaction();
        var result = await CheckAsync();
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("SQLite database is not writable.", result.Description);
        Assert.Equal(5, Assert.IsType<SqliteException>(result.Exception).SqliteErrorCode);
    }

    [Fact]
    public async Task CheckHealthAsync_CancellationPropagatesWithoutCreatingDatabase()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var check = CreateCheck(_tempDirectory, DatabasePath);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            check.CheckHealthAsync(new HealthCheckContext(), cancellation.Token));
        Assert.False(File.Exists(DatabasePath));
        Assert.Empty(Directory.GetFiles(_tempDirectory));
    }

    [Fact]
    public async Task CheckHealthAsync_CancellationAtDatabaseOpenPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        var check = CreateCheck(_tempDirectory, DatabasePath, onConnectionCreated: cancellation.Cancel);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            check.CheckHealthAsync(new HealthCheckContext(), cancellation.Token));
        Assert.False(File.Exists(DatabasePath));
    }

    [Fact]
    public async Task CheckHealthAsync_Fails_WhenKeyStorageIsAFile()
    {
        var keysPath = Path.Combine(_tempDirectory, "keys-file");
        await File.WriteAllTextAsync(keysPath, "not a writable directory", TestContext.Current.CancellationToken);
        var check = CreateCheck(keysPath, DatabasePath);
        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Data Protection key directory is unavailable.", result.Description);
        Assert.False(File.Exists(DatabasePath));
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Requires Windows directory ACLs.")]
    public async Task CheckHealthAsync_Fails_WhenKeyDirectoryDisallowsFileCreation()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        var directory = new DirectoryInfo(_tempDirectory);
        var originalSecurity = directory.GetAccessControl();
        var restrictedSecurity = directory.GetAccessControl();
        using var identity = WindowsIdentity.GetCurrent();
        restrictedSecurity.AddAccessRule(new FileSystemAccessRule(
            identity.User!, FileSystemRights.CreateFiles, AccessControlType.Deny));
        try
        {
            directory.SetAccessControl(restrictedSecurity);
            var result = await CheckAsync();
            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Equal("Data Protection key directory is not writable.", result.Description);
            Assert.IsType<UnauthorizedAccessException>(result.Exception);
            Assert.False(File.Exists(DatabasePath));
        }
        finally
        {
            directory.SetAccessControl(originalSecurity);
        }
    }

    [Fact]
    public async Task CheckHealthAsync_Fails_WhenDataProtectionDirectoryIsMissing()
    {
        var missingKeysPath = Path.Combine(_tempDirectory, "missing");
        var databasePath = Path.Combine(_tempDirectory, "ready.db");
        var check = CreateCheck(missingKeysPath, databasePath);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    public void Dispose()
    {
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private Task<HealthCheckResult> CheckAsync() => CreateCheck(_tempDirectory, DatabasePath)
        .CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

    private LocalReadinessHealthCheck CreateCheck(
        string keysPath,
        string databasePath,
        SqliteOpenMode mode = SqliteOpenMode.ReadWriteCreate,
        Action? onConnectionCreated = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ =>
        {
            onConnectionCreated?.Invoke();
            return new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = mode,
                Pooling = false
            }.ToString());
        });
        var provider = services.BuildServiceProvider();
        _providers.Add(provider);

        return new LocalReadinessHealthCheck(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new DataProtectionSettings("RandomSteamGame", keysPath),
            Microsoft.Extensions.Options.Options.Create(new ApplicationOptions()));
    }
}
