using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RandomSteamGame.Options;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using RandomSteamGame.Shared.Interfaces;
using SteamApiClient.Services;
using SteamApiClient.Settings;
using System.Collections.Concurrent;

namespace RandomSteamGame.Tests;

public sealed class OwnedGamesRefreshConcurrencyTests
{
    private const long SteamId = 76561197960287930L;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task SameIdAcrossScopesInvalidatesOnceAndCommitsCooldownOnce()
    {
        using var fixture = new Fixture();
        using var firstScope = fixture.Services.CreateScope();
        using var secondScope = fixture.Services.CreateScope();
        Assert.NotSame(firstScope.ServiceProvider.GetRequiredService<IOwnedGamesCacheResetTracker>(),
            secondScope.ServiceProvider.GetRequiredService<IOwnedGamesCacheResetTracker>());
        var entered = Signal();
        var release = Signal();
        fixture.Provider.Invalidate = async (_, _, ct) => { entered.TrySetResult(); await release.Task.WaitAsync(ct); };
        var first = firstScope.ServiceProvider.GetRequiredService<GameApplicationService>().RefreshLibraryAsync("steam", SteamId, Ct);
        await entered.Task.WaitAsync(Ct);
        var second = secondScope.ServiceProvider.GetRequiredService<GameApplicationService>().RefreshLibraryAsync("steam", SteamId, Ct);
        Assert.False(second.IsCompleted);
        Assert.Equal(1, fixture.Provider.Calls);
        Assert.Equal(1, fixture.Cache.Reads);
        Assert.Equal(0, fixture.Cache.Writes);
        release.SetResult();

        Assert.Null((await first.WaitAsync(Ct)).Value.NextAvailableAt);
        Assert.Equal(fixture.Now.AddHours(12), (await second.WaitAsync(Ct)).Value.NextAvailableAt);
        Assert.Equal(1, fixture.Provider.Calls);
        Assert.Equal(1, fixture.Cache.Writes);
        Assert.Equal(2, fixture.Cache.Reads);
        Assert.Equal($"owned_games_cache_reset_{SteamId}", Assert.Single(fixture.Cache.Values).Key);
        Assert.Equal(720, fixture.Cache.LastPolicy!.AbsoluteMinutes);
        Assert.Equal(Ct, fixture.Cache.ReadToken);
        Assert.Equal(Ct, fixture.Cache.WriteToken);
        Assert.Equal(0, fixture.Admission.ActiveKeyCount);
    }

    [Fact]
    public async Task DifferentIdsCanInvalidateConcurrently()
    {
        using var fixture = new Fixture();
        var bothEntered = Signal();
        var release = Signal();
        fixture.Provider.Invalidate = async (_, call, ct) =>
        {
            if (call == 2) bothEntered.TrySetResult();
            await release.Task.WaitAsync(ct);
        };
        using var firstScope = fixture.Services.CreateScope();
        using var secondScope = fixture.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<GameApplicationService>().RefreshLibraryAsync("steam", SteamId, Ct);
        var second = secondScope.ServiceProvider.GetRequiredService<GameApplicationService>().RefreshLibraryAsync("steam", SteamId + 1, Ct);
        await bothEntered.Task.WaitAsync(Ct);
        Assert.Equal(2, fixture.Admission.ActiveKeyCount);
        Assert.Equal(0, fixture.Cache.Writes);
        release.SetResult();
        Assert.Null((await first.WaitAsync(Ct)).Value.NextAvailableAt);
        Assert.Null((await second.WaitAsync(Ct)).Value.NextAvailableAt);
        Assert.Equal(2, fixture.Cache.Writes);
        Assert.Equal(0, fixture.Admission.ActiveKeyCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedOrCanceledWinnerReleasesAdmissionForWaitingRequest(bool canceled)
    {
        using var fixture = new Fixture();
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var entered = Signal();
        var release = Signal();
        fixture.Provider.Invalidate = async (_, call, ct) =>
        {
            if (call != 1) return;
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
            throw new InvalidOperationException("Invalidation failed.");
        };
        using var firstScope = fixture.Services.CreateScope();
        using var secondScope = fixture.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<GameApplicationService>().RefreshLibraryAsync("steam", SteamId, caller.Token);
        await entered.Task.WaitAsync(Ct);
        var second = secondScope.ServiceProvider.GetRequiredService<GameApplicationService>().RefreshLibraryAsync("steam", SteamId, Ct);
        Assert.False(second.IsCompleted);
        if (canceled)
        {
            caller.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.WaitAsync(Ct));
        }
        else
        {
            release.SetResult();
            await Assert.ThrowsAsync<InvalidOperationException>(() => first.WaitAsync(Ct));
        }
        Assert.Null((await second.WaitAsync(Ct)).Value.NextAvailableAt);
        Assert.Equal(2, fixture.Provider.Calls);
        Assert.Equal(1, fixture.Cache.WriteAttempts);
        Assert.Equal(1, fixture.Cache.Writes);
        Assert.Equal(0, fixture.Admission.ActiveKeyCount);
    }

    [Fact]
    public async Task CanceledWaiterDoesNoWorkAndDoesNotLeakAdmission()
    {
        using var fixture = new Fixture();
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var entered = Signal();
        var release = Signal();
        fixture.Provider.Invalidate = async (_, _, ct) => { entered.TrySetResult(); await release.Task.WaitAsync(ct); };
        using var firstScope = fixture.Services.CreateScope();
        using var secondScope = fixture.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<GameApplicationService>().RefreshLibraryAsync("steam", SteamId, Ct);
        await entered.Task.WaitAsync(Ct);
        var second = secondScope.ServiceProvider.GetRequiredService<GameApplicationService>().RefreshLibraryAsync("steam", SteamId, caller.Token);
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second.WaitAsync(Ct));
        Assert.Equal(1, fixture.Provider.Calls);
        Assert.Equal(1, fixture.Cache.Reads);
        Assert.Equal(0, fixture.Cache.WriteAttempts);
        Assert.Equal(1, fixture.Admission.ActiveKeyCount);
        release.SetResult();
        Assert.Null((await first.WaitAsync(Ct)).Value.NextAvailableAt);
        Assert.Equal(1, fixture.Cache.Writes);
        Assert.Equal(0, fixture.Admission.ActiveKeyCount);
    }

    [Fact]
    public async Task ExistingPersistedCooldownRejectsWithoutInvalidatingOrWriting()
    {
        using var fixture = new Fixture();
        fixture.Cache.Values[$"owned_games_cache_reset_{SteamId}"] = fixture.Now.AddHours(-2);
        using var scope = fixture.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<GameApplicationService>().RefreshLibraryAsync("steam", SteamId, Ct);
        Assert.Equal(fixture.Now.AddHours(10), result.Value.NextAvailableAt);
        Assert.NotNull(result.Value.CooldownProblem);
        Assert.Equal(0, fixture.Provider.Calls);
        Assert.Equal(0, fixture.Cache.WriteAttempts);
        Assert.Equal(0, fixture.Admission.ActiveKeyCount);
    }

    [Fact]
    public async Task CommitFailurePropagatesAndWaitingRequestMayRetryInvalidation()
    {
        using var fixture = new Fixture();
        fixture.Cache.FailNextWrite = 1;
        var entered = Signal();
        var release = Signal();
        fixture.Provider.Invalidate = async (_, call, ct) =>
        {
            if (call != 1) return;
            entered.TrySetResult();
            await release.Task.WaitAsync(ct);
        };
        using var firstScope = fixture.Services.CreateScope();
        using var secondScope = fixture.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<GameApplicationService>().RefreshLibraryAsync("steam", SteamId, Ct);
        await entered.Task.WaitAsync(Ct);
        var second = secondScope.ServiceProvider.GetRequiredService<GameApplicationService>().RefreshLibraryAsync("steam", SteamId, Ct);
        release.SetResult();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => first.WaitAsync(Ct));
        Assert.Equal("Cooldown persistence failed.", failure.Message);
        Assert.Null((await second.WaitAsync(Ct)).Value.NextAvailableAt);
        Assert.Equal(2, fixture.Provider.Calls);
        Assert.Equal(2, fixture.Cache.WriteAttempts);
        Assert.Equal(1, fixture.Cache.Writes);
        Assert.Equal(0, fixture.Admission.ActiveKeyCount);
    }

    [Fact]
    public async Task CooldownReadFailureReleasesAdmission()
    {
        using var fixture = new Fixture();
        fixture.Cache.FailNextRead = 1;
        using var scope = fixture.Services.CreateScope();
        var operations = scope.ServiceProvider.GetRequiredService<GameApplicationService>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => operations.RefreshLibraryAsync("steam", SteamId, Ct));
        Assert.Equal(0, fixture.Provider.Calls);
        Assert.Equal(0, fixture.Admission.ActiveKeyCount);
        Assert.Null((await operations.RefreshLibraryAsync("steam", SteamId, Ct)).Value.NextAvailableAt);
        Assert.Equal(1, fixture.Provider.Calls);
        Assert.Equal(0, fixture.Admission.ActiveKeyCount);
    }

    private sealed class Fixture : IDisposable, IDateTimeProvider
    {
        public DateTimeOffset Now { get; } = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
        public DateTime UtcNow => Now.UtcDateTime;
        public TestCache Cache { get; } = new();
        public TestProvider Provider { get; } = new();
        public ServiceProvider Services { get; }
        public OwnedGamesRefreshAdmissionCoordinator Admission => Services.GetRequiredService<OwnedGamesRefreshAdmissionCoordinator>();

        public Fixture()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ICacheService>(Cache);
            services.AddSingleton<IDateTimeProvider>(this);
            services.AddSingleton<OwnedGamesRefreshAdmissionCoordinator>();
            services.AddScoped<IOwnedGamesCacheResetTracker, OwnedGamesCacheResetTracker>();
            services.AddScoped(provider => new GameApplicationService(new GameProviderFactory([Provider]),
                provider.GetRequiredService<IOwnedGamesCacheResetTracker>(), null!, null!, null!,
                Microsoft.Extensions.Options.Options.Create(new ApplicationOptions()), NullLogger<GameApplicationService>.Instance));
            Services = services.BuildServiceProvider();
        }
        public void Dispose() => Services.Dispose();
    }

    private sealed class TestProvider : IGameProvider
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);
        public Func<long, int, CancellationToken, Task> Invalidate { get; set; } = (_, _, _) => Task.CompletedTask;
        public string ProviderKey => "steam";
        public Task InvalidateOwnedGamesCacheAsync(long userId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Invalidate(userId, Interlocked.Increment(ref _calls), ct);
        }
        public Task<ErrorOr<OwnedGamesResponse>> GetOwnedGamesAsync(long userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ErrorOr<GameDetails>> GetRandomGameDetailsAsync(long userId, bool unplayedOnly = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RandomGamePickAttempt> GetRandomGamePickAsync(long userId, bool unplayedOnly = false, IReadOnlyCollection<int>? excludedGameIds = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ErrorOr<long>> ResolveIdentifierAsync(string identifier, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class TestCache : ICacheService
    {
        public ConcurrentDictionary<string, DateTimeOffset> Values { get; } = new();
        private int _reads, _writes, _writeAttempts;
        public int Reads => Volatile.Read(ref _reads);
        public int Writes => Volatile.Read(ref _writes);
        public int WriteAttempts => Volatile.Read(ref _writeAttempts);
        public int FailNextWrite;
        public int FailNextRead;
        public CachePolicy? LastPolicy { get; private set; }
        public CancellationToken ReadToken { get; private set; }
        public CancellationToken WriteToken { get; private set; }

        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            ReadToken = ct;
            Interlocked.Increment(ref _reads);
            if (Interlocked.Exchange(ref FailNextRead, 0) == 1) throw new InvalidOperationException("Cooldown read failed.");
            return Task.FromResult(Values.TryGetValue(key, out var value) ? (T?)(object)value : default);
        }
        public Task SetAsync<T>(string key, T value, CachePolicy policy, IEnumerable<string>? tags = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            WriteToken = ct;
            LastPolicy = policy;
            Interlocked.Increment(ref _writeAttempts);
            if (Interlocked.Exchange(ref FailNextWrite, 0) == 1) throw new InvalidOperationException("Cooldown persistence failed.");
            Values[key] = (DateTimeOffset)(object)value!;
            Interlocked.Increment(ref _writes);
            return Task.CompletedTask;
        }
        public Task<T> CoalesceAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CachePolicy policy, IEnumerable<string>? tags = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CacheLookupResult<T>> GetOrCreateWithMetadataAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CachePolicy policy, IEnumerable<string>? tags = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task InvalidateByTagAsync(string tag, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
