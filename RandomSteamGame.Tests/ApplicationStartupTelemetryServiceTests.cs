using JoyfulReaperLib.MissionControl;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using RandomSteamGame.Events;
using RandomSteamGame.Options;
using RandomSteamGame.Services;

namespace RandomSteamGame.Tests;

public class ApplicationStartupTelemetryServiceTests
{
    [Fact]
    public async Task StartAsync_PublishesExactlyOneStartupEvent_WithDeploymentMetadata()
    {
        var missionControl = new RecordingMissionControlClient();
        var service = CreateService(
            missionControl,
            new ApplicationOptions
            {
                CommitSha = "1fc5721778e1",
                DeploymentType = "docker"
            });

        await service.StartAsync(CancellationToken.None);

        var published = Assert.Single(missionControl.PublishedEvents);
        Assert.Equal(RandomSteamGameEventTypes.ApplicationStarted, published.EventType);
        var payload = Assert.IsType<ApplicationStartedEvent>(published.Payload);
        Assert.Equal("Production", payload.Environment);
        Assert.Equal("1fc5721778e1", payload.CommitSha);
        Assert.Equal("docker", payload.DeploymentType);
        Assert.NotEmpty(payload.FrameworkVersion);
    }

    [Fact]
    public async Task StartAsync_MissingDeploymentMetadata_DoesNotThrow()
    {
        var missionControl = new RecordingMissionControlClient();
        var service = CreateService(missionControl, new ApplicationOptions());

        await service.StartAsync(CancellationToken.None);

        var payload = Assert.IsType<ApplicationStartedEvent>(
            Assert.Single(missionControl.PublishedEvents).Payload);
        Assert.Null(payload.CommitSha);
        Assert.Null(payload.DeploymentType);
    }

    [Fact]
    public async Task StartAsync_MissionControlFailure_DoesNotThrow()
    {
        var missionControl = new RecordingMissionControlClient
        {
            ExceptionToThrow = new InvalidOperationException("Mission Control unavailable.")
        };
        var service = CreateService(missionControl, new ApplicationOptions());

        await service.StartAsync(CancellationToken.None);

        Assert.Single(missionControl.PublishedEvents);
    }

    private static ApplicationStartupTelemetryService CreateService(
        RecordingMissionControlClient missionControl,
        ApplicationOptions options)
        => new(
            missionControl,
            new FakeHostEnvironment(),
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<ApplicationStartupTelemetryService>.Instance);

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "RandomSteamGame";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class RecordingMissionControlClient : IMissionControlClient
    {
        public List<PublishedEventRecord> PublishedEvents { get; } = [];
        public Exception? ExceptionToThrow { get; init; }

        public Task<bool> TryPublishAsync<TPayload>(
            string eventType,
            TPayload payload,
            DateTimeOffset occurredAt,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            PublishedEvents.Add(new PublishedEventRecord(
                eventType,
                payload!,
                occurredAt,
                correlationId));

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(true);
        }
    }

    private sealed record PublishedEventRecord(
        string EventType,
        object Payload,
        DateTimeOffset OccurredAt,
        string? CorrelationId);
}
