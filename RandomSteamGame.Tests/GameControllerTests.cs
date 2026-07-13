using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using RandomSteamGame.Common.Errors;
using RandomSteamGame.Controllers;
using RandomSteamGame.Events;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using System.Reflection;
using System.Text;
using JoyfulReaperLib.MissionControl;

namespace RandomSteamGame.Tests;

public class GameControllerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-76561197960287930L)]
    [InlineData(123456789)]
    public async Task GetLibrary_InvalidSteamId_ReturnsValidationProblem(long steamId)
    {
        var controller = CreateController();

        var result = await controller.GetLibrary("steam", steamId);

        AssertValidationProblem(result, "Steam.InvalidSteamId");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999999999999999L)]
    public async Task RefreshLibrary_InvalidSteamId_ReturnsValidationProblem(long steamId)
    {
        var controller = CreateController();

        var result = await controller.RefreshLibrary("steam", steamId);

        AssertValidationProblem(result, "Steam.InvalidSteamId");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(123456789)]
    public async Task ExportLibrary_InvalidSteamId_ReturnsValidationProblem(long steamId)
    {
        var controller = CreateController();

        var result = await controller.ExportLibrary("steam", steamId);

        AssertValidationProblem(result, "Steam.InvalidSteamId");
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("has.dot")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("https://steamcommunity.com/profiles/76561197960287930/")]
    public async Task ResolveVanity_InvalidVanityUrl_ReturnsValidationProblem(string vanityUrl)
    {
        var controller = CreateController();

        var result = await controller.ResolveVanity("steam", vanityUrl);

        AssertValidationProblem(result, "Steam.InvalidVanityUrl");
    }

    [Theory]
    [InlineData("Mister_God")]
    [InlineData("https://steamcommunity.com/id/Mister_God/")]
    [InlineData("http://steamcommunity.com/id/Mister_God")]
    [InlineData("steamcommunity.com/id/Mister_God")]
    [InlineData("www.steamcommunity.com/id/Mister_God/")]
    [InlineData("https:%2F%2Fsteamcommunity.com%2Fid%2FMister_God%2F")]
    public async Task ResolveVanity_AcceptsNormalizedVanityVariants(string vanityUrl)
    {
        var controller = CreateController();

        var result = await controller.ResolveVanity("steam", vanityUrl);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(76561197960287930L, Assert.IsType<long>(ok.Value));
    }

    [Fact]
    public async Task GetRandomGameDetails_UnsupportedProvider_ReturnsProblem_AndPublishesFailureEvent()
    {
        var missionControl = new RecordingMissionControlClient();
        var controller = CreateController(missionControlClient: missionControl);

        var result = await controller.GetRandomGameDetails("gog", 76561197960287930L, vanityUrl: null);

        AssertValidationProblem(result, "Steam.UnsupportedProvider");
        AssertGamePickEvent(
            missionControl,
            expectedProvider: "gog",
            expectedOutcome: "unsupported-provider",
            expectedSucceeded: false,
            expectedAppId: null,
            expectedUnplayedOnly: false);
    }

    [Fact]
    public async Task GetRandomGameDetails_InvalidSteamId_PublishesInvalidIdentifierEvent()
    {
        var missionControl = new RecordingMissionControlClient();
        var controller = CreateController(missionControlClient: missionControl);

        var result = await controller.GetRandomGameDetails("steam", 123456789, vanityUrl: null);

        AssertValidationProblem(result, "Steam.InvalidSteamId");
        AssertGamePickEvent(
            missionControl,
            expectedProvider: "steam",
            expectedOutcome: "invalid-identifier",
            expectedSucceeded: false,
            expectedAppId: null,
            expectedUnplayedOnly: false);
    }

    [Fact]
    public async Task GetRandomGameDetails_InvalidVanityInput_PublishesInvalidIdentifierEvent()
    {
        var missionControl = new RecordingMissionControlClient();
        var controller = CreateController(missionControlClient: missionControl);

        var result = await controller.GetRandomGameDetails("steam", userId: null, vanityUrl: "has space");

        AssertValidationProblem(result, "Steam.InvalidVanityUrl");
        AssertGamePickEvent(
            missionControl,
            expectedProvider: "steam",
            expectedOutcome: "invalid-identifier",
            expectedSucceeded: false,
            expectedAppId: null,
            expectedUnplayedOnly: false);
    }

    [Fact]
    public async Task GetRandomGameDetails_MissingIdentifier_PublishesInvalidIdentifierEvent()
    {
        var missionControl = new RecordingMissionControlClient();
        var appStats = new FakeAppStatsService();
        var provider = new FakeGameProvider();
        var controller = CreateController(provider, appStats, missionControl);

        var result = await controller.GetRandomGameDetails("steam", userId: null, vanityUrl: null);

        AssertValidationProblem(result, "Steam.IdentifierRequired");
        Assert.Single(missionControl.PublishedEvents);
        AssertGamePickEvent(
            missionControl,
            expectedProvider: "steam",
            expectedOutcome: "invalid-identifier",
            expectedSucceeded: false,
            expectedAppId: null,
            expectedUnplayedOnly: false);
        Assert.Equal(0, provider.ResolveIdentifierCallCount);
        Assert.Equal(0, provider.GetRandomGameDetailsCallCount);
        Assert.Equal(0, appStats.IncrementCallCount);
    }

    [Fact]
    public async Task GetRandomGameDetails_AmbiguousIdentifier_PublishesInvalidIdentifierEvent()
    {
        var missionControl = new RecordingMissionControlClient();
        var appStats = new FakeAppStatsService();
        var provider = new FakeGameProvider();
        var controller = CreateController(provider, appStats, missionControl);

        var result = await controller.GetRandomGameDetails(
            "steam",
            76561197960287930L,
            vanityUrl: "Mister_God");

        AssertValidationProblem(result, "Steam.AmbiguousIdentifier");
        Assert.Single(missionControl.PublishedEvents);
        AssertGamePickEvent(
            missionControl,
            expectedProvider: "steam",
            expectedOutcome: "invalid-identifier",
            expectedSucceeded: false,
            expectedAppId: null,
            expectedUnplayedOnly: false);
        Assert.Equal(0, provider.ResolveIdentifierCallCount);
        Assert.Equal(0, provider.GetRandomGameDetailsCallCount);
        Assert.Equal(0, appStats.IncrementCallCount);
    }

    [Fact]
    public async Task GetRandomGameDetails_VanityResolutionFailure_PublishesIdentifierResolutionFailedEvent()
    {
        var missionControl = new RecordingMissionControlClient();
        var provider = new FakeGameProvider(resolveIdentifierResult: Errors.Steam.VanityResolutionFailed);
        var controller = CreateController(provider, missionControlClient: missionControl);

        var result = await controller.GetRandomGameDetails("steam", userId: null, vanityUrl: "Mister_God");

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        AssertGamePickEvent(
            missionControl,
            expectedProvider: "steam",
            expectedOutcome: "identifier-resolution-failed",
            expectedSucceeded: false,
            expectedAppId: null,
            expectedUnplayedOnly: false);
    }

    [Fact]
    public async Task GetRandomGameDetails_ProviderSelectionFailure_PublishesSelectionFailedEvent()
    {
        var missionControl = new RecordingMissionControlClient();
        var provider = new FakeGameProvider(randomGameResult: Errors.Steam.SteamApiFailed);
        var controller = CreateController(provider, missionControlClient: missionControl);

        var result = await controller.GetRandomGameDetails("steam", 76561197960287930L, vanityUrl: null);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
        AssertGamePickEvent(
            missionControl,
            expectedProvider: "steam",
            expectedOutcome: "selection-failed",
            expectedSucceeded: false,
            expectedAppId: null,
            expectedUnplayedOnly: false);
    }

    [Fact]
    public async Task GetRandomGameDetails_Success_ReturnsGameDetails_IncrementsStats_AndPublishesOneSuccessEvent()
    {
        var missionControl = new RecordingMissionControlClient();
        var appStats = new FakeAppStatsService();
        var provider = new FakeGameProvider(
            randomGameResult: new GameDetails
            {
                Id = 42,
                Name = "Portal",
                Description = "Test description",
                HeaderImage = "header.png"
            });
        var controller = CreateController(provider, appStats, missionControl);

        var result = await controller.GetRandomGameDetails("steam", 76561197960287930L, vanityUrl: null, unplayedOnly: true);

        var ok = Assert.IsType<OkObjectResult>(result);
        var game = Assert.IsType<GameDetails>(ok.Value);
        Assert.Equal(42, game.Id);
        Assert.Equal("Portal", game.Name);
        Assert.Equal(1, appStats.IncrementCallCount);
        Assert.Single(missionControl.PublishedEvents);

        AssertGamePickEvent(
            missionControl,
            expectedProvider: "steam",
            expectedOutcome: "served",
            expectedSucceeded: true,
            expectedAppId: 42,
            expectedUnplayedOnly: true);
    }

    [Fact]
    public async Task GetRandomGameDetails_MissionControlFailure_DoesNotChangeSuccessfulResponse()
    {
        var missionControl = new RecordingMissionControlClient
        {
            ExceptionToThrow = new InvalidOperationException("Mission Control unavailable.")
        };
        var provider = new FakeGameProvider(
            randomGameResult: new GameDetails
            {
                Id = 42,
                Name = "Portal"
            });
        var controller = CreateController(provider, missionControlClient: missionControl);

        var result = await controller.GetRandomGameDetails("steam", 76561197960287930L, vanityUrl: null);

        var ok = Assert.IsType<OkObjectResult>(result);
        var game = Assert.IsType<GameDetails>(ok.Value);
        Assert.Equal(42, game.Id);
        Assert.Single(missionControl.PublishedEvents);
    }

    [Fact]
    public async Task GetRandomGameDetails_AppStatsIncrementFailure_DoesNotChangeSuccessfulResponse()
    {
        var missionControl = new RecordingMissionControlClient();
        var appStats = new FakeAppStatsService
        {
            ThrowOnIncrement = true
        };
        var provider = new FakeGameProvider(
            randomGameResult: new GameDetails
            {
                Id = 42,
                Name = "Portal"
            });
        var controller = CreateController(provider, appStats, missionControl);

        var result = await controller.GetRandomGameDetails("steam", 76561197960287930L, vanityUrl: null);

        var ok = Assert.IsType<OkObjectResult>(result);
        var game = Assert.IsType<GameDetails>(ok.Value);
        Assert.Equal(42, game.Id);
        Assert.Equal(1, appStats.IncrementCallCount);
        Assert.Single(missionControl.PublishedEvents);
        AssertGamePickEvent(
            missionControl,
            expectedProvider: "steam",
            expectedOutcome: "served",
            expectedSucceeded: true,
            expectedAppId: 42,
            expectedUnplayedOnly: false);
    }

    [Fact]
    public void GamePickCompletedEvent_DoesNotExposeSteamIdentifiersOrSecrets()
    {
        var propertyNames = typeof(GamePickCompletedEvent)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(propertyNames, name => name.Contains("Steam", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Cookie", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Key", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Owned", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Library", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GameController_UsesRateLimitingPolicy_ForCacheRefreshEndpoint()
    {
        var controllerAttribute = typeof(GameController).GetCustomAttribute<EnableRateLimitingAttribute>();
        var action = typeof(GameController).GetMethod(nameof(GameController.RefreshLibrary));

        Assert.NotNull(action);
        Assert.NotNull(controllerAttribute);
        Assert.Equal("steam_api_limiter", controllerAttribute.PolicyName);
    }

    [Fact]
    public void GameController_UsesRateLimitingPolicy_ForLibraryExportEndpoint()
    {
        var controllerAttribute = typeof(GameController).GetCustomAttribute<EnableRateLimitingAttribute>();
        var action = typeof(GameController).GetMethod(nameof(GameController.ExportLibrary));

        Assert.NotNull(action);
        Assert.NotNull(controllerAttribute);
        Assert.Equal("steam_api_limiter", controllerAttribute.PolicyName);
    }

    [Fact]
    public void HitController_UsesRateLimitingPolicy()
    {
        var attribute = typeof(HitController).GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("steam_api_limiter", attribute.PolicyName);
    }

    [Fact]
    public async Task ExportLibrary_Success_ReturnsCsvFile()
    {
        const long steamId = 76561197960287930L;
        var controller = CreateController(new FakeGameProvider(new OwnedGamesResponse(steamId, 1, [
            new Game(10, "Portal", 90, null, 0, 0, 0, 0, 0)
        ])));

        var result = await controller.ExportLibrary("steam", steamId);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.Equal($"steam-library-{steamId}.csv", file.FileDownloadName);
        Assert.Equal("game,id,hours,last_played,steam_deck\r\nPortal,10,1.5,,unknown\r\n", Encoding.UTF8.GetString(file.FileContents));
    }

    private static GameController CreateController(
        FakeGameProvider? provider = null,
        FakeAppStatsService? appStatsService = null,
        RecordingMissionControlClient? missionControlClient = null)
    {
        var controller = new GameController(
            new GameProviderFactory([provider ?? new FakeGameProvider()]),
            new FakeOwnedGamesCacheResetTracker(),
            appStatsService ?? new FakeAppStatsService(),
            new SteamLibraryExportService(),
            missionControlClient ?? new RecordingMissionControlClient(),
            NullLogger<GameController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private static void AssertValidationProblem(IActionResult result, string expectedErrorCode)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ValidationProblemDetails>(objectResult.Value);

        Assert.Contains(expectedErrorCode, problem.Errors.Keys);
    }

    private static void AssertGamePickEvent(
        RecordingMissionControlClient missionControl,
        string expectedProvider,
        string expectedOutcome,
        bool expectedSucceeded,
        int? expectedAppId,
        bool expectedUnplayedOnly)
    {
        Assert.Single(missionControl.PublishedEvents);

        var publishedEvent = missionControl.PublishedEvents[0];
        Assert.Equal(RandomSteamGameEventTypes.GamePickCompleted, publishedEvent.EventType);
        Assert.Equal(expectedProvider, publishedEvent.Payload.Provider);
        Assert.Equal(expectedAppId, publishedEvent.Payload.AppId);
        Assert.Equal(expectedUnplayedOnly, publishedEvent.Payload.UnplayedOnly);
        Assert.Equal(expectedOutcome, publishedEvent.Payload.Outcome);
        Assert.Equal(expectedSucceeded, publishedEvent.Payload.Succeeded);
        Assert.True(publishedEvent.Payload.DurationMilliseconds >= 0);
    }

    private sealed class FakeGameProvider : IGameProvider
    {
        private readonly OwnedGamesResponse _library;
        private readonly ErrorOr<GameDetails> _randomGameResult;
        private readonly ErrorOr<long> _resolveIdentifierResult;

        public int GetOwnedGamesCallCount { get; private set; }
        public int GetRandomGameDetailsCallCount { get; private set; }
        public int ResolveIdentifierCallCount { get; private set; }

        public FakeGameProvider()
            : this(
                new OwnedGamesResponse(76561197960287930L, 0, []),
                randomGameResult: new GameDetails { Id = 1, Name = "Test" },
                resolveIdentifierResult: 76561197960287930L)
        {
        }

        public FakeGameProvider(
            OwnedGamesResponse? library = null,
            ErrorOr<GameDetails>? randomGameResult = null,
            ErrorOr<long>? resolveIdentifierResult = null)
        {
            _library = library ?? new OwnedGamesResponse(76561197960287930L, 0, []);
            _randomGameResult = randomGameResult ?? new GameDetails { Id = 1, Name = "Test" };
            _resolveIdentifierResult = resolveIdentifierResult ?? 76561197960287930L;
        }

        public string ProviderKey => "steam";

        public Task<ErrorOr<OwnedGamesResponse>> GetOwnedGamesAsync(long userId)
        {
            GetOwnedGamesCallCount++;
            return Task.FromResult<ErrorOr<OwnedGamesResponse>>(_library with { SteamId = userId });
        }

        public Task<ErrorOr<GameDetails>> GetRandomGameDetailsAsync(long userId, bool unplayedOnly = false)
        {
            GetRandomGameDetailsCallCount++;
            return Task.FromResult(_randomGameResult);
        }

        public Task<ErrorOr<long>> ResolveIdentifierAsync(string identifier)
        {
            ResolveIdentifierCallCount++;
            return Task.FromResult(_resolveIdentifierResult);
        }

        public Task InvalidateOwnedGamesCacheAsync(long userId)
            => Task.CompletedTask;
    }

    private sealed class FakeOwnedGamesCacheResetTracker : IOwnedGamesCacheResetTracker
    {
        public Task<DateTimeOffset?> GetNextAvailableAtAsync(long steamId)
            => Task.FromResult<DateTimeOffset?>(null);

        public Task MarkResetAsync(long steamId)
            => Task.CompletedTask;
    }

    private sealed class FakeAppStatsService : IAppStatsService
    {
        public int IncrementCallCount { get; private set; }
        public bool ThrowOnIncrement { get; init; }

        public Task<AppStatsResponse> RecordHitAsync(string ip)
            => Task.FromResult(new AppStatsResponse(0, 0, 0));

        public Task<AppStatsResponse> GetStatsAsync()
            => Task.FromResult(new AppStatsResponse(0, 0, 0));

        public Task IncrementRandomGamesGeneratedAsync()
        {
            IncrementCallCount++;

            if (ThrowOnIncrement)
            {
                throw new InvalidOperationException("App stats store unavailable.");
            }

            return Task.CompletedTask;
        }
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
                payload is GamePickCompletedEvent completed
                    ? completed
                    : throw new InvalidOperationException("Unexpected payload type."),
                occurredAt,
                correlationId ?? string.Empty));

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(true);
        }
    }

    private sealed record PublishedEventRecord(
        string EventType,
        GamePickCompletedEvent Payload,
        DateTimeOffset OccurredAt,
        string CorrelationId);
}
