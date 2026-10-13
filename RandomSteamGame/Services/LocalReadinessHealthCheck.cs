using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RandomSteamGame.Extensions;
using RandomSteamGame.Options;

namespace RandomSteamGame.Services;

internal sealed class LocalReadinessHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DataProtectionSettings _dataProtectionSettings;
    private readonly IOptions<ApplicationOptions> _applicationOptions;

    public LocalReadinessHealthCheck(
        IServiceScopeFactory scopeFactory,
        DataProtectionSettings dataProtectionSettings,
        IOptions<ApplicationOptions> applicationOptions)
    {
        _scopeFactory = scopeFactory;
        _dataProtectionSettings = dataProtectionSettings;
        _applicationOptions = applicationOptions;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_applicationOptions.Value is null)
        {
            return HealthCheckResult.Unhealthy("Application configuration is unavailable.");
        }

        if (!Directory.Exists(_dataProtectionSettings.KeysPath))
        {
            return HealthCheckResult.Unhealthy("Data Protection key directory is unavailable.");
        }

        try
        {
            var probePath = Path.Combine(
                _dataProtectionSettings.KeysPath,
                $".readiness-{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(probePath, string.Empty, cancellationToken);
            File.Delete(probePath);
        }
        catch (Exception exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return HealthCheckResult.Unhealthy("Data Protection key directory is not writable.", exception);
        }

        var failureDescription = "SQLite database is unavailable.";
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var scope = _scopeFactory.CreateScope();
            var connection = scope.ServiceProvider.GetRequiredService<SqliteConnection>();
            // Only this readiness scope uses a short busy timeout, including transaction commands.
            connection.DefaultTimeout = 1;
            await connection.OpenAsync(cancellationToken);

            failureDescription = "Required SQLite schema is unavailable.";
            if (!await HasRequiredColumnsAsync(connection, "Visitors", ["IpAddress", "Hits", "LastSeen"], cancellationToken)
                || !await HasRequiredColumnsAsync(connection, "AppStats", ["Id", "RandomGamesGenerated", "LibrariesExported"], cancellationToken))
            {
                return HealthCheckResult.Unhealthy(failureDescription);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM AppStats WHERE Id = 1;";
            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 1)
            {
                return HealthCheckResult.Unhealthy("Required SQLite bootstrap row is unavailable.");
            }

            failureDescription = "SQLite database is not writable.";
            cancellationToken.ThrowIfCancellationRequested();
            using var transaction = connection.BeginTransaction();
            command.Transaction = transaction;
            // Exercise the actual database write path without incrementing any counters.
            // Disposal also rolls back if execution fails or the caller cancels.
            command.CommandText = "UPDATE AppStats SET RandomGamesGenerated = RandomGamesGenerated WHERE Id = 1;";
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                return HealthCheckResult.Unhealthy("Required SQLite bootstrap row is unavailable.");
            }
            transaction.Rollback();
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (Exception exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return HealthCheckResult.Unhealthy(failureDescription, exception);
        }

        return HealthCheckResult.Healthy();
    }

    private static async Task<bool> HasRequiredColumnsAsync(
        SqliteConnection connection,
        string table,
        string[] requiredColumns,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_schema WHERE type = 'table' AND name = $table;";
        command.Parameters.AddWithValue("$table", table);
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 1)
        {
            return false;
        }

        command.CommandText = "SELECT name FROM pragma_table_info($table);";
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }
        return requiredColumns.All(columns.Contains);
    }
}
