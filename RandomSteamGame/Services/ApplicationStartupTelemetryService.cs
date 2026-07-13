using JoyfulReaperLib.MissionControl;
using Microsoft.Extensions.Options;
using RandomSteamGame.Events;
using RandomSteamGame.Options;
using System.Runtime.InteropServices;

namespace RandomSteamGame.Services;

public sealed class ApplicationStartupTelemetryService : IHostedLifecycleService
{
    private readonly IMissionControlClient _missionControlClient;
    private readonly IHostEnvironment _environment;
    private readonly ApplicationOptions _applicationOptions;
    private readonly ILogger<ApplicationStartupTelemetryService> _logger;
    private int _published;

    public ApplicationStartupTelemetryService(
        IMissionControlClient missionControlClient,
        IHostEnvironment environment,
        IOptions<ApplicationOptions> applicationOptions,
        ILogger<ApplicationStartupTelemetryService> logger)
    {
        _missionControlClient = missionControlClient;
        _environment = environment;
        _applicationOptions = applicationOptions.Value;
        _logger = logger;
    }

    public Task StartingAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public async Task StartedAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _published, 1) == 1)
        {
            return;
        }

        if (_environment.IsProduction() &&
            string.IsNullOrWhiteSpace(_applicationOptions.CommitSha))
        {
            _logger.LogWarning("Application commit SHA is not configured.");
        }

        try
        {
            await _missionControlClient.TryPublishAsync(
                RandomSteamGameEventTypes.ApplicationStarted,
                new ApplicationStartedEvent(
                    _environment.EnvironmentName,
                    NullIfWhiteSpace(_applicationOptions.CommitSha),
                    NullIfWhiteSpace(_applicationOptions.DeploymentType),
                    RuntimeInformation.FrameworkDescription),
                DateTimeOffset.UtcNow,
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to publish application startup event.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
