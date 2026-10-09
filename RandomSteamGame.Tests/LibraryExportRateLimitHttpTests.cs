using ErrorOr;
using JoyfulReaperLib.MissionControl;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RandomSteamGame.Common.Errors;
using RandomSteamGame.Events;
using RandomSteamGame.Options;
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

    [Fact]
    public async Task GlobalExport_SimultaneousRequestIsRejectedAndCooldownStartsAtAdmission()
    {
        var clock = new ManualTimeProvider();
        var provider = new BlockingExportGameProvider();
        using var application = CreateGlobalApplication(provider, clock);
        using var client = application.CreateClient();

        var firstTask = SendExportAsync(client, "198.51.100.70");
        var secondTask = SendExportAsync(client, "198.51.100.71");
        Task<HttpResponseMessage>? acceptedTask = null;
        try
        {
            await provider.WaitUntilFirstStartedAsync();
            var rejectedTask = await Task.WhenAny(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            acceptedTask = rejectedTask == firstTask ? secondTask : firstTask;
            using var rejected = await rejectedTask;
            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            // Rejection may run before the winner reserves its cooldown; both hints are practical.
            Assert.True(rejected.Headers.RetryAfter?.Delta is { } hint && hint > TimeSpan.Zero && hint <= TimeSpan.FromMinutes(20));
            Assert.Equal(1, provider.CallCount);
            Assert.Contains("capacity", await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            using var running = await SendExportAsync(client, "198.51.100.74");
            Assert.Equal(HttpStatusCode.TooManyRequests, running.StatusCode);
            Assert.Equal(TimeSpan.FromMinutes(20), running.Headers.RetryAfter?.Delta);

            // Expired cooldown does not free the concurrency permit while Steam work is blocked.
            clock.Advance(TimeSpan.FromMinutes(21));
            using var stillBusy = await SendExportAsync(client, "198.51.100.72");
            Assert.Equal(HttpStatusCode.TooManyRequests, stillBusy.StatusCode);
            Assert.Equal(TimeSpan.FromSeconds(5), stillBusy.Headers.RetryAfter?.Delta);
            Assert.Equal(1, provider.CallCount);
        }
        finally
        {
            provider.Release();
        }
        using var accepted = await acceptedTask!;
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        // Completion did not restart the cooldown. It expired while generation was in progress.
        using var next = await SendExportAsync(client, "198.51.100.73");
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task GlobalExport_CooldownRejectsAllVisitorsUntilConfiguredExpiry()
    {
        var clock = new ManualTimeProvider();
        using var application = CreateGlobalApplication(new ExportGameProvider(), clock, cooldownMinutes: 7);
        using var client = application.CreateClient();
        using var first = await SendExportAsync(client, "198.51.100.80");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await SendExportAsync(client, "198.51.100.81", SteamId + 1);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal(TimeSpan.FromMinutes(7), second.Headers.RetryAfter?.Delta);
        Assert.Contains("global cooldown", await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(second.Headers.CacheControl?.Private);
        Assert.True(second.Headers.CacheControl?.NoStore);

        clock.Advance(TimeSpan.FromMinutes(7) - TimeSpan.FromMilliseconds(500));
        using var nearlyReady = await SendExportAsync(client, "198.51.100.82");
        Assert.Equal(HttpStatusCode.TooManyRequests, nearlyReady.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(1), nearlyReady.Headers.RetryAfter?.Delta);

        clock.Advance(TimeSpan.FromMilliseconds(500));
        using var ready = await SendExportAsync(client, "198.51.100.83");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        using var page = await client.GetAsync("/library-export", TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("7-minute cooldown", html);
        Assert.DoesNotContain("One export per IP address every 72 hours", html);
    }

    [Fact]
    public async Task GlobalExport_FailureStillConsumesCooldown()
    {
        var clock = new ManualTimeProvider();
        using var application = CreateGlobalApplication(new FailingThenSuccessfulExportGameProvider(), clock);
        using var client = application.CreateClient();
        using var failed = await SendExportAsync(client, "198.51.100.90");
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        using var retry = await SendExportAsync(client, "198.51.100.91");
        Assert.Equal(HttpStatusCode.TooManyRequests, retry.StatusCode);
        Assert.Equal(TimeSpan.FromMinutes(20), retry.Headers.RetryAfter?.Delta);
        clock.Advance(TimeSpan.FromMinutes(20));
        using var ready = await SendExportAsync(client, "198.51.100.92");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public void GlobalCooldownReservationIsAtomic()
    {
        var tracker = new GlobalLibraryExportCooldownTracker(
            Microsoft.Extensions.Options.Options.Create(new LibraryExportOptions
            {
                RateLimitMode = LibraryExportRateLimitMode.Global
            }), new ManualTimeProvider());
        var results = new TimeSpan?[32];
        Parallel.For(0, results.Length, index => results[index] = tracker.TryStart());
        Assert.Single(results, result => result is null);
        Assert.Equal(31, results.Count(result => result == TimeSpan.FromMinutes(20)));
    }

    private WebApplicationFactory<Program> CreateGlobalApplication(
        IGameProvider provider, TimeProvider clock, int cooldownMinutes = 20) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:NetworkMode"] = "AltNet",
                ["Application:CanonicalOrigin"] = "http://example.b32.i2p",
                ["Steam:LibraryExport:RateLimitMode"] = "Global",
                ["Steam:LibraryExport:GlobalCooldownMinutes"] = cooldownMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                // Global mode must still enforce one generation if PerIp capacity is greater than one.
                ["Steam:LibraryExport:GlobalConcurrency"] = "32"
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGameProvider>();
                services.AddSingleton(provider);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(clock);
            });
        });

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
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
        public int CallCount => Volatile.Read(ref _callCount);

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
                bool unplayedOnly = false,
                CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<RandomGamePickAttempt>
            GetRandomGamePickAsync(
                long userId,
                bool unplayedOnly = false,
                IReadOnlyCollection<int>? excludedGameIds = null, CancellationToken ct = default)
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
                bool unplayedOnly = false,
                CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<RandomGamePickAttempt>
            GetRandomGamePickAsync(
                long userId,
                bool unplayedOnly = false,
                IReadOnlyCollection<int>? excludedGameIds = null, CancellationToken ct = default)
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
                bool unplayedOnly = false,
                CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<RandomGamePickAttempt>
            GetRandomGamePickAsync(
                long userId,
                bool unplayedOnly = false,
                IReadOnlyCollection<int>? excludedGameIds = null, CancellationToken ct = default)
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
