using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Timeout;
using RandomSteamGame.Common.Errors;
using RandomSteamGame.Options;
using RandomSteamGame.Services;
using RandomSteamGame.Shared.Services;
using SteamApiClient.Contracts.SteamApi;
using SteamApiClient.Contracts.SteamStoreApi;
using SteamApiClient.HttpClients;
using SteamApiClient.Services;
using System.Text.Json;

namespace RandomSteamGame.Tests;

public class SteamProviderTests
{
    private const long SteamId = 76561197960287930L;
    private static readonly OwnedGamesCacheInfo CacheInfo = new(OwnedGamesCacheStatus.Hit, 42);

    [Fact]
    public async Task StoreUnavailableCandidate_TriesAnotherCandidateSuccessfully()
    {
        var calls = 0;
        var store = new StubStoreClient((appId, _) => Task.FromResult(
            ++calls == 1 ? null : CreateAppData(appId)));
        var provider = CreateProvider(store);

        var result = await provider.GetRandomGamePickAsync(SteamId, ct: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(2, store.RequestedAppIds.Count);
        Assert.NotEqual(store.RequestedAppIds[0], store.RequestedAppIds[1]);
        Assert.Equal(store.RequestedAppIds[1], result.Game!.Id);
        Assert.Equal(CacheInfo, result.Cache);
    }

    [Theory]
    [InlineData("transport")]
    [InlineData("json")]
    [InlineData("response-shape")]
    [InlineData("resilience-timeout")]
    [InlineData("http-timeout")]
    [InlineData("timeout")]
    [InlineData("circuit-open")]
    [InlineData("rate-limit")]
    public async Task StoreDependencyFailure_ReturnsControlledFailureWithoutTryingAnotherCandidate(string failure)
    {
        Exception exception = failure switch
        {
            "transport" => new HttpRequestException("Steam Store transport failed."),
            "json" => new JsonException("Malformed Steam Store JSON."),
            "response-shape" => new InvalidOperationException("JSON root is not an object."),
            "resilience-timeout" => new TimeoutRejectedException(),
            "http-timeout" => new TaskCanceledException("HttpClient timed out."),
            "timeout" => new TimeoutException(),
            "circuit-open" => new BrokenCircuitException(),
            "rate-limit" => new RateLimiterRejectedException(),
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };
        var store = new StubStoreClient((_, _) => Task.FromException<AppData?>(exception));
        var logger = new RecordingLogger();
        var provider = CreateProvider(store, logger);

        var result = await provider.GetRandomGamePickAsync(SteamId, ct: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(Errors.Steam.SteamApiFailed, Assert.Single(result.Errors));
        var appId = Assert.Single(store.RequestedAppIds);
        var log = Assert.Single(logger.Warnings);
        Assert.Same(exception, log.Exception);
        Assert.Contains($"AppId {appId}", log.Message);
        Assert.Equal(CacheInfo, result.Cache);
        Assert.Equal(2, result.EligibleGameCount);
        Assert.Equal(2, result.LibraryGameCount);
        Assert.True(result.Timings.LibraryLoadMilliseconds >= 0);
        Assert.True(result.Timings.SelectionMilliseconds >= 0);
    }

    [Fact]
    public async Task StoreCallerCancellation_PropagatesWithoutTryingAnotherCandidate()
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var store = new StubStoreClient((_, ct) =>
        {
            Assert.Equal(caller.Token, ct);
            caller.Cancel();
            return Task.FromCanceled<AppData?>(ct);
        });
        var logger = new RecordingLogger();
        var provider = CreateProvider(store, logger);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GetRandomGamePickAsync(SteamId, ct: caller.Token));

        Assert.Single(store.RequestedAppIds);
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public async Task LibraryCallerCancellation_PropagatesBeforeStoreSelection()
    {
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var steam = new StubSteamClient
        {
            LoadLibrary = ct =>
            {
                Assert.Equal(caller.Token, ct);
                caller.Cancel();
                return Task.FromCanceled<OwnedGamesResult>(ct);
            }
        };
        var store = new StubStoreClient((appId, _) => Task.FromResult<AppData?>(CreateAppData(appId)));
        var provider = CreateProvider(store, steam: steam);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.FetchRandomGamePickAsync(SteamId, ct: caller.Token));

        Assert.Empty(store.RequestedAppIds);
    }

    [Fact]
    public void IsUnplayed_ReturnsTrue_WhenAllSteamPlaySignalsAreZero()
    {
        var game = CreateGame();

        Assert.True(SteamProvider.IsUnplayed(game));
    }

    [Theory]
    [InlineData(15, 0, 0, 0, 0, 0)]
    [InlineData(0, 20, 0, 0, 0, 0)]
    [InlineData(0, 0, 25, 0, 0, 0)]
    [InlineData(0, 0, 0, 30, 0, 0)]
    [InlineData(0, 0, 0, 0, 35, 0)]
    [InlineData(0, 0, 0, 0, 0, 1234567890)]
    public void IsUnplayed_ReturnsFalse_WhenAnySteamPlaySignalShowsActivity(
        int playtimeForever,
        int playtimeWindowsForever,
        int playtimeMacForever,
        int playtimeLinuxForever,
        int playtime2Weeks,
        long rTimeLastPlayed)
    {
        var game = CreateGame(
            playtimeForever: playtimeForever,
            playtimeWindowsForever: playtimeWindowsForever,
            playtimeMacForever: playtimeMacForever,
            playtimeLinuxForever: playtimeLinuxForever,
            playtime2Weeks: playtime2Weeks,
            rTimeLastPlayed: rTimeLastPlayed);

        Assert.False(SteamProvider.IsUnplayed(game));
    }

    [Theory]
    [InlineData(120, 0, 0, 0, 0, 120)]
    [InlineData(0, 80, 40, 0, 0, 120)]
    [InlineData(60, 80, 40, 0, 0, 120)]
    [InlineData(0, 0, 0, 0, 45, 45)]
    public void GetDisplayPlaytimeMinutes_UsesBestAvailableSteamPlaytimeSignal(
        int playtimeForever,
        int playtimeWindowsForever,
        int playtimeMacForever,
        int playtimeLinuxForever,
        int playtime2Weeks,
        int expectedMinutes)
    {
        Assert.Equal(
            expectedMinutes,
            SteamPlaytimeHelper.GetDisplayPlaytimeMinutes(
                playtimeForever,
                playtimeWindowsForever,
                playtimeMacForever,
                playtimeLinuxForever,
                playtime2Weeks));
    }

    private static SteamProvider CreateProvider(ISteamStoreClient store, RecordingLogger? logger = null,
        StubSteamClient? steam = null)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new ApplicationOptions());
        return new SteamProvider(steam ?? new StubSteamClient(), store, new HttpContextAccessor(),
            new HtmlSanitizerService(options), logger ?? new RecordingLogger(), options);
    }

    private static AppData CreateAppData(int appId) => JsonSerializer.Deserialize<AppData>(
        JsonSerializer.Serialize(new
        {
            type = "game", steam_appid = appId, name = $"Game{appId}",
            about_the_game = "A game description.", header_image = ""
        }))!;

    private sealed class StubStoreClient(Func<int, CancellationToken, Task<AppData?>> getAppData) : ISteamStoreClient
    {
        public List<int> RequestedAppIds { get; } = [];

        public Task<AppData?> GetAppData(int appId, IEnumerable<string>? tags = null, CancellationToken ct = default)
        {
            RequestedAppIds.Add(appId);
            return getAppData(appId, ct);
        }
    }

    private sealed class StubSteamClient : ISteamClient
    {
        private static readonly OwnedGames Library = new(2, [CreateGame(), CreateGame() with { AppId = 20 }]);
        public Func<CancellationToken, Task<OwnedGamesResult>> LoadLibrary { get; init; } =
            _ => Task.FromResult(new OwnedGamesResult(Library, CacheInfo));

        public Task<OwnedGames> GetOwnedGames(long steamId, bool includeAppInfo = true,
            bool includePlayedFreeGames = true, CancellationToken ct = default) => Task.FromResult(Library);
        public Task<OwnedGamesResult> GetOwnedGamesWithCacheInfo(long steamId, bool includeAppInfo = true,
            bool includePlayedFreeGames = true, CancellationToken ct = default) => LoadLibrary(ct);
        public Task<long> GetSteamIdFromVanityUrl(string vanityUrl, CancellationToken ct = default) => Task.FromResult(SteamId);
        public Task<IReadOnlyDictionary<int, SteamDeckCompatibilityCategory>> GetSteamDeckCompatibilityAsync(
            IEnumerable<int> appIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, SteamDeckCompatibilityCategory>>(new Dictionary<int, SteamDeckCompatibilityCategory>());
        public Task InvalidateOwnedGamesCacheAsync(long steamId) => Task.CompletedTask;
    }

    private sealed class RecordingLogger : ILogger<SteamProvider>
    {
        public List<(Exception? Exception, string Message)> Warnings { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add((exception, formatter(state, exception)));
            }
        }
    }

    private static Game CreateGame(
        int playtimeForever = 0,
        int playtimeWindowsForever = 0,
        int playtimeMacForever = 0,
        int playtimeLinuxForever = 0,
        int playtime2Weeks = 0,
        long rTimeLastPlayed = 0)
    {
        return new Game(
            AppId: 10,
            Name: "Test Game",
            PlaytimeForever: playtimeForever,
            ImgIconUrl: null,
            PlaytimeWindowsForever: playtimeWindowsForever,
            PlaytimeMacForever: playtimeMacForever,
            PlaytimeLinuxForever: playtimeLinuxForever,
            RTimeLastPlayed: rTimeLastPlayed,
            Playtime2Weeks: playtime2Weeks);
    }
}
