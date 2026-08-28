using ErrorOr;
using JoyfulReaperLib.MissionControl;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RandomSteamGame.Common.Errors;
using RandomSteamGame.Events;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using System.Net;
using System.Text.Json.Serialization.Metadata;

namespace RandomSteamGame.Tests;

public sealed class LibraryExportRateLimitHttpTests :
    IClassFixture<SeoWebApplicationFactory>
{
    private const long SteamId = 76561197960287930L;

    private readonly SeoWebApplicationFactory _factory;

    public LibraryExportRateLimitHttpTests(
        SeoWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LibraryExportLimiter_LimitsGlobalConcurrentExports()
    {
        var provider = new BlockingExportGameProvider();
        var missionControl = new RecordingMissionControlClient();

        using var application =
            _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IGameProvider>();
                    services.AddSingleton<IGameProvider>(provider);

                    services.RemoveAll<IMissionControlClient>();
                    services.AddSingleton<IMissionControlClient>(
                        missionControl);
                });
            });

        using var client = application.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var firstTask = SendExportAsync(
            client,
            "198.51.100.70");

        var secondTask = SendExportAsync(
            client,
            "198.51.100.71");

        try
        {
            await provider.WaitUntilTwoStartedAsync();

            using var third = await SendExportAsync(
                client,
                "198.51.100.72");

            Assert.Equal(
                HttpStatusCode.TooManyRequests,
                third.StatusCode);

            var rejectionMessage =
                await third.Content.ReadAsStringAsync(
                    TestContext.Current.CancellationToken);

            Assert.Contains(
                "capacity",
                rejectionMessage,
                StringComparison.OrdinalIgnoreCase);

            var rejected =
                Assert.Single(
                    missionControl.LibraryExportRejectedEvents);

            Assert.Equal(
                RandomSteamGameEventTypes.LibraryExportRejected,
                rejected.EventType);

            Assert.Equal(
                "steam",
                rejected.Payload.Provider);

            Assert.Equal(
                LibraryExportRejectionReason.Capacity,
                rejected.Payload.Reason);

            Assert.Null(
                rejected.Payload.RetryAfterSeconds);
        }
        finally
        {
            provider.Release();
        }

        using var first = await firstTask;
        using var second = await secondTask;

        Assert.Equal(
            HttpStatusCode.OK,
            first.StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            second.StatusCode);
    }

    [Fact]
    public async Task LibraryExportLimiter_UsesConfiguredGlobalConcurrency()
    {
        var provider = new BlockingExportGameProvider();

        using var application =
            _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Steam:LibraryExport:GlobalConcurrency"] = "1"
                        });
                });

                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IGameProvider>();
                    services.AddSingleton<IGameProvider>(provider);
                });
            });

        using var client = application.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var firstTask = SendExportAsync(
            client,
            "198.51.100.70");

        await provider.WaitUntilFirstStartedAsync();

        var secondTask = SendExportAsync(
            client,
            "198.51.100.71");

        try
        {
            var secondEnteredProviderTask =
                provider.WaitUntilTwoStartedAsync();

            var completedTask = await Task.WhenAny(
                secondTask,
                secondEnteredProviderTask);

            Assert.Same(secondTask, completedTask);

            using var second = await secondTask;

            Assert.Equal(
                HttpStatusCode.TooManyRequests,
                second.StatusCode);
        }
        finally
        {
            provider.Release();
        }

        using var first = await firstTask;

        Assert.Equal(
            HttpStatusCode.OK,
            first.StatusCode);
    }

    [Fact]
    public async Task LibraryExportCooldown_IsSharedByUsersBehindSameIp()
    {
        using var application =
            _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IGameProvider>();
                    services.AddScoped<
                        IGameProvider,
                        ExportGameProvider>();
                });
            });

        using var client = application.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        const string sharedIp = "198.51.100.60";
        const long otherSteamId = 76561198000000000L;

        using var first =
            await SendExportAsync(
                client,
                sharedIp,
                SteamId);

        Assert.Equal(
            HttpStatusCode.OK,
            first.StatusCode);

        using var secondUser =
            await SendExportAsync(
                client,
                sharedIp,
                otherSteamId);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            secondUser.StatusCode);
    }

    [Fact]
    public async Task LibraryExportCooldown_FailedAttempt_DoesNotConsumeCooldown()
    {
        using var application =
            _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IGameProvider>();
                    services.AddSingleton<
                        IGameProvider,
                        FailingThenSuccessfulExportGameProvider>();
                });
            });

        using var client = application.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        const string clientIp = "198.51.100.50";

        using var failed =
            await SendExportAsync(client, clientIp);

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            failed.StatusCode);

        using var retry =
            await SendExportAsync(client, clientIp);

        Assert.Equal(
            HttpStatusCode.OK,
            retry.StatusCode);

        using var afterSuccess =
            await SendExportAsync(client, clientIp);

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            afterSuccess.StatusCode);

        Assert.NotNull(afterSuccess.Headers.RetryAfter);
        Assert.NotNull(afterSuccess.Headers.RetryAfter.Delta);

        Assert.True(
            afterSuccess.Headers.RetryAfter.Delta > TimeSpan.Zero);

        Assert.True(
            afterSuccess.Headers.RetryAfter.Delta <=
            TimeSpan.FromHours(72));
    }

    [Fact]
    public async Task LibraryExportLimiter_IsPartitionedByClientIp()
    {
        using var application =
            _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IGameProvider>();
                    services.AddScoped<
                        IGameProvider,
                        ExportGameProvider>();
                });
            });

        using var client = application.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        using var first =
            await SendExportAsync(
                client,
                "198.51.100.10");

        Assert.Equal(
            HttpStatusCode.OK,
            first.StatusCode);

        using var second =
            await SendExportAsync(
                client,
                "198.51.100.10");

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            second.StatusCode);

        var rejectionMessage =
            await second.Content.ReadAsStringAsync(
                TestContext.Current.CancellationToken);

        Assert.Contains(
            "one per IP address every 72 hours",
            rejectionMessage,
            StringComparison.OrdinalIgnoreCase);

        using var differentIp =
            await SendExportAsync(
                client,
                "198.51.100.11");

        Assert.Equal(
            HttpStatusCode.OK,
            differentIp.StatusCode);
    }

    private static Task<HttpResponseMessage> SendExportAsync(
        HttpClient client,
        string forwardedFor,
        long steamId = SteamId)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/steam/{steamId}/library/export.csv");

        request.Headers.TryAddWithoutValidation(
            "X-Forwarded-For",
            forwardedFor);

        return client.SendAsync(
            request,
            TestContext.Current.CancellationToken);
    }

    private sealed class BlockingExportGameProvider : IGameProvider
    {
        private readonly TaskCompletionSource<bool> _twoStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> _firstStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _callCount;

        public string ProviderKey => "steam";

        public async Task WaitUntilTwoStartedAsync()
        {
            await _twoStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        }

        public void Release()
        {
            _release.TrySetResult(true);
        }

        public async Task<ErrorOr<OwnedGamesResponse>>
            GetOwnedGamesAsync(long userId)
        {
            var callCount = Interlocked.Increment(ref _callCount);

            if (callCount == 1)
            {
                _firstStarted.TrySetResult(true);
            }

            if (callCount == 2)
            {
                _twoStarted.TrySetResult(true);
            }

            await _release.Task.WaitAsync(
                TestContext.Current.CancellationToken);

            OwnedGamesResponse library =
                new(
                    userId,
                    1,
                    [
                        new Game(
                            620,
                            "Portal 2",
                            120,
                            null,
                            0,
                            0,
                            0,
                            0,
                            0)
                    ]);

            return library;
        }

        public async Task WaitUntilFirstStartedAsync()
        {
            await _firstStarted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
        }

        public Task<ErrorOr<GameDetails>>
            GetRandomGameDetailsAsync(
                long userId,
                bool unplayedOnly = false)
        {
            throw new NotSupportedException();
        }

        public Task<RandomGamePickAttempt>
            GetRandomGamePickAsync(
                long userId,
                bool unplayedOnly = false)
        {
            throw new NotSupportedException();
        }

        public Task<ErrorOr<long>>
            ResolveIdentifierAsync(string identifier)
        {
            throw new NotSupportedException();
        }

        public Task InvalidateOwnedGamesCacheAsync(long userId)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FailingThenSuccessfulExportGameProvider
        : IGameProvider
    {
        private int _ownedGamesCallCount;

        public string ProviderKey => "steam";

        public Task<ErrorOr<OwnedGamesResponse>>
            GetOwnedGamesAsync(long userId)
        {
            if (Interlocked.Increment(
                    ref _ownedGamesCallCount) == 1)
            {
                return Task.FromResult<ErrorOr<OwnedGamesResponse>>(
                    Errors.Steam.SteamApiFailed);
            }

            OwnedGamesResponse library =
                new(
                    userId,
                    1,
                    [
                        new Game(
                            620,
                            "Portal 2",
                            120,
                            null,
                            0,
                            0,
                            0,
                            0,
                            0)
                    ]);

            return Task.FromResult<
                ErrorOr<OwnedGamesResponse>>(library);
        }

        public Task<ErrorOr<GameDetails>>
            GetRandomGameDetailsAsync(
                long userId,
                bool unplayedOnly = false)
        {
            throw new NotSupportedException();
        }

        public Task<RandomGamePickAttempt>
            GetRandomGamePickAsync(
                long userId,
                bool unplayedOnly = false)
        {
            throw new NotSupportedException();
        }

        public Task<ErrorOr<long>>
            ResolveIdentifierAsync(string identifier)
        {
            throw new NotSupportedException();
        }

        public Task InvalidateOwnedGamesCacheAsync(long userId)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ExportGameProvider : IGameProvider
    {
        public string ProviderKey => "steam";

        public Task<ErrorOr<OwnedGamesResponse>>
            GetOwnedGamesAsync(long userId)
        {
            OwnedGamesResponse library =
                new(
                    userId,
                    1,
                    [
                        new Game(
                            620,
                            "Portal 2",
                            120,
                            null,
                            0,
                            0,
                            0,
                            0,
                            0)
                    ]);

            return Task.FromResult<
                ErrorOr<OwnedGamesResponse>>(library);
        }

        public Task<ErrorOr<GameDetails>>
            GetRandomGameDetailsAsync(
                long userId,
                bool unplayedOnly = false)
        {
            throw new NotSupportedException();
        }

        public Task<RandomGamePickAttempt>
            GetRandomGamePickAsync(
                long userId,
                bool unplayedOnly = false)
        {
            throw new NotSupportedException();
        }

        public Task<ErrorOr<long>>
            ResolveIdentifierAsync(string identifier)
        {
            throw new NotSupportedException();
        }

        public Task InvalidateOwnedGamesCacheAsync(long userId)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMissionControlClient
        : IMissionControlClient
    {
        public List<PublishedLibraryExportRejectedEventRecord>
            LibraryExportRejectedEvents
        { get; } = [];

        public Task<bool> TryPublishAsync<TPayload>(
            string eventType,
            TPayload payload,
            JsonTypeInfo<TPayload> payloadTypeInfo,
            DateTimeOffset occurredAt,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            if (payload is LibraryExportRejectedEvent rejected)
            {
                LibraryExportRejectedEvents.Add(
                    new PublishedLibraryExportRejectedEventRecord(
                        eventType,
                        rejected));
            }

            return Task.FromResult(true);
        }
    }

    private sealed record PublishedLibraryExportRejectedEventRecord(
        string EventType,
        LibraryExportRejectedEvent Payload);
}