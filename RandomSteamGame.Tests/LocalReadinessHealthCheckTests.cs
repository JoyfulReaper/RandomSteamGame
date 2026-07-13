using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RandomSteamGame.Extensions;
using RandomSteamGame.Options;
using RandomSteamGame.Services;

namespace RandomSteamGame.Tests;

public class LocalReadinessHealthCheckTests : IDisposable
{
    private readonly string _tempDirectory;

    public LocalReadinessHealthCheckTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task CheckHealthAsync_Succeeds_WithWritableKeysAndValidSqlite()
    {
        var databasePath = Path.Combine(_tempDirectory, "ready.db");
        var check = CreateCheck(_tempDirectory, databasePath);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
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
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static LocalReadinessHealthCheck CreateCheck(
        string keysPath,
        string databasePath)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => new SqliteConnection($"Data Source={databasePath}"));
        var provider = services.BuildServiceProvider();

        return new LocalReadinessHealthCheck(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new DataProtectionSettings("RandomSteamGame", keysPath),
            Microsoft.Extensions.Options.Options.Create(new ApplicationOptions()));
    }
}
