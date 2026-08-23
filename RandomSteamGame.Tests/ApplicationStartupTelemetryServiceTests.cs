using JoyfulReaperLib.MissionControl;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using RandomSteamGame.Events;
using RandomSteamGame.Options;
using RandomSteamGame.Services;
using System.Text.Json.Serialization.Metadata;

namespace RandomSteamGame.Tests;

public class ApplicationStartupTelemetryServiceTests
{
    [Fact]
    public async Task StartedAsync_PublishesExactlyOneStartupEvent_WithDeploymentMetadata()
    {
        var missionControl = new RecordingMissionControlClient();
        var service = CreateService(
            missionControl,
            new ApplicationOptions
            {
                CommitSha = "1fc5721778e1",
                DeploymentType = "docker"
            });

        await service.StartedAsync(CancellationToken.None);
        await service.StartedAsync(CancellationToken.None);

        var published = Assert.Single(missionControl.PublishedEvents);
        Assert.Equal(RandomSteamGameEventTypes.ApplicationStarted, published.EventType);
        var payload = Assert.IsType<ApplicationStartedEvent>(published.Payload);
        Assert.Equal("Production", payload.Environment);
        Assert.Equal("1fc5721778e1", payload.CommitSha);
        Assert.Equal("docker", payload.DeploymentType);
        Assert.NotEmpty(payload.FrameworkVersion);
    }

    [Fact]
    public async Task StartedAsync_MissingDeploymentMetadata_DoesNotThrow()
    {
        var missionControl = new RecordingMissionControlClient();
        var service = CreateService(missionControl, new ApplicationOptions());

        await service.StartedAsync(CancellationToken.None);

        var payload = Assert.IsType<ApplicationStartedEvent>(
            Assert.Single(missionControl.PublishedEvents).Payload);
        Assert.Null(payload.CommitSha);
        Assert.Null(payload.DeploymentType);
    }

    [Fact]
    public async Task StartedAsync_MissionControlFailure_DoesNotThrow()
    {
        var missionControl = new RecordingMissionControlClient
        {
            ExceptionToThrow = new InvalidOperationException("Mission Control unavailable.")
        };
        var service = CreateService(missionControl, new ApplicationOptions());

        await service.StartedAsync(CancellationToken.None);

        Assert.Single(missionControl.PublishedEvents);
    }

    [Fact]
    public async Task StartAsync_DoesNotPublishStartupEvent()
    {
        var missionControl = new RecordingMissionControlClient();
        var service = CreateService(missionControl, new ApplicationOptions());

        await service.StartAsync(CancellationToken.None);

        Assert.Empty(missionControl.PublishedEvents);
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
        public List<PublishedLibraryExportEventRecord> LibraryExportEvents { get; } = [];
        public Exception? ExceptionToThrow { get; init; }

        public Task<bool> TryPublishAsync<TPayload>(
            string eventType,
            TPayload payload,
            JsonTypeInfo<TPayload> payloadTypeInfo,
            DateTimeOffset occurredAt,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            switch (payload)
            {
                case ApplicationStartedEvent applicationStarted:
                    PublishedEvents.Add(
                        new PublishedEventRecord(
                            eventType,
                            applicationStarted,
                            occurredAt,
                            correlationId));
                    break;

                case GamePickCompletedEvent gamePick:
                    PublishedEvents.Add(
                        new PublishedEventRecord(
                            eventType,
                            gamePick,
                            occurredAt,
                            correlationId ?? string.Empty));
                    break;

                case LibraryExportCompletedEvent libraryExport:
                    LibraryExportEvents.Add(
                        new PublishedLibraryExportEventRecord(
                            eventType,
                            libraryExport,
                            occurredAt,
                            correlationId ?? string.Empty));
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unexpected payload type: {typeof(TPayload).Name}");
            }

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

    private sealed record PublishedLibraryExportEventRecord(
        string EventType,
        LibraryExportCompletedEvent Payload,
        DateTimeOffset OccurredAt,
        string CorrelationId);
}
