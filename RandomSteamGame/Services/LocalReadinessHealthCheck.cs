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
            return HealthCheckResult.Unhealthy("Data Protection key directory is not writable.", exception);
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var connection = scope.ServiceProvider.GetRequiredService<SqliteConnection>();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            await command.ExecuteScalarAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("SQLite database is unavailable.", exception);
        }

        return HealthCheckResult.Healthy();
    }
}
