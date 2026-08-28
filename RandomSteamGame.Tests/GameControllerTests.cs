using ErrorOr;
using JoyfulReaperLib.MissionControl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using RandomSteamGame.Common.Errors;
using RandomSteamGame.Controllers;
using RandomSteamGame.Events;
using RandomSteamGame.Options;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using SteamApiClient.Services;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using SteamDeckCompatibilityCategory = SteamApiClient.Contracts.SteamApi.SteamDeckCompatibilityCategory;

namespace RandomSteamGame.Tests;

public class GameControllerTests
{
    [Fact]
    public async Task ExportLibrary_Cooldown_PublishesLibraryExportRejectedEvent()
    {
        const long steamId = 76561197960287930L;

        var missionControl = new RecordingMissionControlClient();

        var cooldownTracker = new FakeLibraryExportCooldownTracker
        {
            RetryAfter = TimeSpan.FromHours(12)
        };

        var controller = CreateController(
            missionControlClient: missionControl,
            applicationOptions: new ApplicationOptions
            {
                CommitSha = "abc123"
            },
            remoteIpAddress: "192.0.2.42",
            libraryExportCooldownTracker: cooldownTracker);

        var result = await controller.ExportLibrary(
            "steam",
            steamId);

        var content = Assert.IsType<ContentResult>(result);

        Assert.Equal(
            StatusCodes.Status429TooManyRequests,
            content.StatusCode);

        Assert.Equal(
            "43200",
            controller.Response.Headers.RetryAfter.ToString());

        var published =
            Assert.Single(missionControl.LibraryExportRejectedEvents);

        Assert.Equal(
            RandomSteamGameEventTypes.LibraryExportRejected,
            published.EventType);

        Assert.Equal(
            "hashed-192.0.2.42",
            published.Payload.VisitorId);

        Assert.Equal(
            "steam",
            published.Payload.Provider);

        Assert.Equal(
            LibraryExportRejectionReason.Cooldown,
            published.Payload.Reason);

        Assert.Equal(
            43200,
            published.Payload.RetryAfterSeconds);

        Assert.Equal(
            "abc123",
            published.Payload.CommitSha);
    }

    [Fact]
    public async Task ExportLibrary_DisablesBrowserAndCdnCaching()
    {
        const long steamId = 76561197960287930L;

        var controller = CreateController(
            new FakeGameProvider(
                new OwnedGamesResponse(
                    steamId,
                    1,
                    [
                        new Game(
                        10,
                        "Portal",
                        90,
                        null,
                        0,
                        0,
                        0,
                        0,
                        0)
                    ])));

        var result = await controller.ExportLibrary("steam", steamId);

        Assert.IsType<FileContentResult>(result);

        Assert.Equal(
            "private, no-store",
            controller.Response.Headers["Cache-Control"].ToString());

        Assert.Equal(
            "no-store",
            controller.Response.Headers["CDN-Cache-Control"].ToString());
    }

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

    [Fact]
    public async Task ExportLibrary_Success_PublishesLibraryExportEvent()
    {
        const long steamId = 76561197960287930L;

        var missionControl = new RecordingMissionControlClient();

        var provider = new FakeGameProvider(
            library: new OwnedGamesResponse(
                steamId,
                4,
                [
                    new Game(10, "Verified Game", 60, null, 0, 0, 0, 0, 0),
                new Game(20, "Playable Game", 60, null, 0, 0, 0, 0, 0),
                new Game(30, "Unsupported Game", 60, null, 0, 0, 0, 0, 0),
                new Game(40, "Unknown Game", 60, null, 0, 0, 0, 0, 0)
                ]),
            deckCompatibility: new Dictionary<int, SteamDeckCompatibilityCategory>
            {
                [10] = SteamDeckCompatibilityCategory.Verified,
                [20] = SteamDeckCompatibilityCategory.Playable,
                [30] = SteamDeckCompatibilityCategory.Unsupported
            });

        var controller = CreateController(
            provider,
            missionControlClient: missionControl,
            applicationOptions: new ApplicationOptions
            {
                CommitSha = "abc123"
            },
            remoteIpAddress: "192.0.2.42");

        var result = await controller.ExportLibrary("steam", steamId);

        Assert.IsType<FileContentResult>(result);

        var published = Assert.Single(missionControl.LibraryExportEvents);

        Assert.Equal(
            RandomSteamGameEventTypes.LibraryExportCompleted,
            published.EventType);

        Assert.Equal("hashed-192.0.2.42", published.Payload.VisitorId);
        Assert.Equal("steam", published.Payload.Provider);
        Assert.Equal(4, published.Payload.GameCount);
        Assert.Equal(1, published.Payload.VerifiedCount);
        Assert.Equal(1, published.Payload.PlayableCount);
        Assert.Equal(1, published.Payload.UnsupportedCount);
        Assert.Equal(1, published.Payload.UnknownCount);
        Assert.Equal("abc123", published.Payload.CommitSha);
        Assert.True(published.Payload.DurationMilliseconds >= 0);

        Assert.Equal(
            published.Payload.GameCount,
            published.Payload.VerifiedCount +
            published.Payload.PlayableCount +
            published.Payload.UnsupportedCount +
            published.Payload.UnknownCount);
    }

    [Fact]
    public async Task ExportLibrary_MissionControlFailure_DoesNotChangeSuccessfulResponse()
    {
        const long steamId = 76561197960287930L;

        var missionControl = new RecordingMissionControlClient
        {
            ExceptionToThrow =
                new InvalidOperationException("Mission Control unavailable.")
        };

        var provider = new FakeGameProvider(
            new OwnedGamesResponse(
                steamId,
                1,
                [
                    new Game(10, "Portal", 90, null, 0, 0, 0, 0, 0)
                ]));

        var controller = CreateController(
            provider,
            missionControlClient: missionControl);

        var result = await controller.ExportLibrary("steam", steamId);

        var file = Assert.IsType<FileContentResult>(result);

        Assert.Equal(
            $"steam-library-{steamId}.csv",
            file.FileDownloadName);

        Assert.Single(missionControl.LibraryExportEvents);
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
            expectedOutcome: "library-load-failed",
            expectedSucceeded: false,
            expectedAppId: null,
            expectedUnplayedOnly: false);
    }

    [Fact]
    public async Task GetRandomGameDetails_NoEligibleGames_PublishesLibraryMetadata()
    {
        var missionControl = new RecordingMissionControlClient();
        var provider = new FakeGameProvider(
            randomGameResult: Errors.Steam.NoSelectableGamesAfterExclusions,
            cacheInfo: new OwnedGamesCacheInfo(OwnedGamesCacheStatus.Hit, 10),
            eligibleGameCount: 0,
            libraryGameCount: 25);
        var controller = CreateController(provider, missionControlClient: missionControl);

        var result = await controller.GetRandomGameDetails("steam", 76561197960287930L, vanityUrl: null);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        var payload = missionControl.PublishedEvents.Single().Payload;
        Assert.Equal("no-eligible-games", payload.Outcome);
        Assert.Equal("hit", payload.CacheStatus);
        Assert.Equal(10, payload.CacheAgeSeconds);
        Assert.Equal(0, payload.EligibleGameCount);
        Assert.Equal("25-99", payload.LibrarySizeBucket);
        Assert.True(payload.Timings.LibraryLoadMilliseconds >= 0);
        Assert.True(payload.Timings.SelectionMilliseconds >= 0);
    }

    [Fact]
    public async Task GetRandomGameDetails_EmptyLibrary_PublishesZeroLibraryMetadata()
    {
        var missionControl = new RecordingMissionControlClient();
        var provider = new FakeGameProvider(
            randomGameResult: Errors.Steam.EmptyLibrary,
            cacheInfo: new OwnedGamesCacheInfo(OwnedGamesCacheStatus.Miss, 0),
            eligibleGameCount: 0,
            libraryGameCount: 0);
        var controller = CreateController(provider, missionControlClient: missionControl);

        var result = await controller.GetRandomGameDetails("steam", 76561197960287930L, vanityUrl: null);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        var payload = missionControl.PublishedEvents.Single().Payload;
        Assert.Equal("empty-library", payload.Outcome);
        Assert.Equal("miss", payload.CacheStatus);
        Assert.Equal(0, payload.EligibleGameCount);
        Assert.Equal("0", payload.LibrarySizeBucket);
        Assert.True(payload.Timings.LibraryLoadMilliseconds >= 0);
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
            expectedUnplayedOnly: true,
            expectedGameName: "Portal");
    }

    [Fact]
    public async Task GetRandomGameDetails_Success_PublishesCacheLibraryTimingAndCommitTelemetry()
    {
        var missionControl = new RecordingMissionControlClient();
        var provider = new FakeGameProvider(
            randomGameResult: new GameDetails
            {
                Id = 42,
                Name = "Portal"
            },
            cacheInfo: new OwnedGamesCacheInfo(OwnedGamesCacheStatus.Hit, 1842),
            eligibleGameCount: 3,
            libraryGameCount: 500);
        var controller = CreateController(
            provider,
            missionControlClient: missionControl,
            applicationOptions: new ApplicationOptions
            {
                CommitSha = "1fc5721778e1",
                DeploymentType = "docker"
            });

        var result = await controller.GetRandomGameDetails("steam", 76561197960287930L, vanityUrl: null);

        Assert.IsType<OkObjectResult>(result);
        var payload = missionControl.PublishedEvents.Single().Payload;
        Assert.Equal("hit", payload.CacheStatus);
        Assert.Equal("Portal", payload.GameName);
        Assert.Equal(1842, payload.CacheAgeSeconds);
        Assert.Equal(3, payload.EligibleGameCount);
        Assert.Equal("500-999", payload.LibrarySizeBucket);
        Assert.Equal("1fc5721778e1", payload.CommitSha);
        Assert.True(payload.Timings.IdentifierResolutionMilliseconds >= 0);
        Assert.True(payload.Timings.LibraryLoadMilliseconds >= 0);
        Assert.True(payload.Timings.SelectionMilliseconds >= 0);
    }

    [Fact]
    public async Task GetRandomGameDetails_CacheMiss_PublishesMissStatus()
    {
        var missionControl = new RecordingMissionControlClient();
        var provider = new FakeGameProvider(
            randomGameResult: new GameDetails
            {
                Id = 42,
                Name = "Portal"
            },
            cacheInfo: new OwnedGamesCacheInfo(OwnedGamesCacheStatus.Miss, 0));
        var controller = CreateController(provider, missionControlClient: missionControl);

        var result = await controller.GetRandomGameDetails("steam", 76561197960287930L, vanityUrl: null);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("miss", missionControl.PublishedEvents.Single().Payload.CacheStatus);
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1-24")]
    [InlineData(24, "1-24")]
    [InlineData(25, "25-99")]
    [InlineData(99, "25-99")]
    [InlineData(100, "100-249")]
    [InlineData(249, "100-249")]
    [InlineData(250, "250-499")]
    [InlineData(499, "250-499")]
    [InlineData(500, "500-999")]
    [InlineData(999, "500-999")]
    [InlineData(1000, "1000+")]
    public void LibrarySizeBuckets_ReturnExpectedBucket(int gameCount, string expectedBucket)
    {
        Assert.Equal(expectedBucket, LibrarySizeBuckets.FromCount(gameCount));
    }

    [Fact]
    public async Task GetRandomGameDetails_SerializedEvent_ExcludesSensitiveInputs()
    {
        var missionControl = new RecordingMissionControlClient();
        var provider = new FakeGameProvider(
            randomGameResult: new GameDetails
            {
                Id = 42,
                Name = "Portal"
            });
        var controller = CreateController(provider, missionControlClient: missionControl);

        await controller.GetRandomGameDetails("steam", 76561197960287930L, vanityUrl: null);

        var json = JsonSerializer.Serialize(
            missionControl.PublishedEvents.Single().Payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.DoesNotContain("76561197960287930", json);
        Assert.Contains("Portal", json);
        Assert.DoesNotContain("steamId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vanityUrl", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apiKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cookie", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ownedGames", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stackTrace", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("RimWorld", "RimWorld")]
    [InlineData("  RimWorld  ", "RimWorld")]
    [InlineData("Game\r\nName", "Game Name")]
    [InlineData("Game\tName", "Game Name")]
    [InlineData("Game   Name", "Game Name")]
    [InlineData("Game\u0001Name", "GameName")]
    [InlineData("Game\u001b[31mName", "GameName")]
    [InlineData("Game\u200EName", "GameName")]
    [InlineData("日本語ゲーム", "日本語ゲーム")]
    public void GamePickTelemetryName_Sanitize_ReturnsExpectedValue(
        string? value,
        string? expected)
    {
        Assert.Equal(expected, GamePickTelemetryName.Sanitize(value));
    }

    [Fact]
    public void GamePickTelemetryName_Sanitize_TruncatesToMaximumLength()
    {
        var sanitized = GamePickTelemetryName.Sanitize(new string('A', 300));

        Assert.NotNull(sanitized);
        Assert.True(sanitized.Length <= 256);
    }

    [Fact]
    public async Task GetRandomGameDetails_PublishesSanitizedGameName()
    {
        var missionControl = new RecordingMissionControlClient();
        var provider = new FakeGameProvider(
            randomGameResult: new GameDetails
            {
                Id = 42,
                Name = "  Game\r\n\u001b[31mName\t "
            });
        var controller = CreateController(provider, missionControlClient: missionControl);

        await controller.GetRandomGameDetails("steam", 76561197960287930L, vanityUrl: null);

        Assert.Equal("Game Name", missionControl.PublishedEvents.Single().Payload.GameName);
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
            expectedUnplayedOnly: false,
            expectedGameName: "Portal");
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
    public void GameController_UsesDedicatedRateLimitingPolicy_ForLibraryExportEndpoint()
    {
        var action =
            typeof(GameController)
                .GetMethod(nameof(GameController.ExportLibrary));

        Assert.NotNull(action);

        var attribute = action.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(attribute);

        Assert.Equal(
            "library_export_limiter",
            attribute.PolicyName);
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
        RecordingMissionControlClient? missionControlClient = null,
        ApplicationOptions? applicationOptions = null,
        string? remoteIpAddress = null,
        ILibraryExportCooldownTracker? libraryExportCooldownTracker = null)
    {
        var controller = new GameController(
            new GameProviderFactory([provider ?? new FakeGameProvider()]),
            new FakeOwnedGamesCacheResetTracker(),
            appStatsService ?? new FakeAppStatsService(),
            new SteamLibraryExportService(),
            missionControlClient ?? new RecordingMissionControlClient(),
            new StubVisitorIdProvider(),
            libraryExportCooldownTracker ?? new FakeLibraryExportCooldownTracker(),
            Microsoft.Extensions.Options.Options.Create(
                applicationOptions ?? new ApplicationOptions()),
            NullLogger<GameController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        if (remoteIpAddress is not null)
        {
            controller.HttpContext.Connection.RemoteIpAddress =
                IPAddress.Parse(remoteIpAddress);
        }

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
        bool expectedUnplayedOnly,
        string? expectedGameName = null)
    {
        Assert.Single(missionControl.PublishedEvents);

        var publishedEvent = missionControl.PublishedEvents[0];
        Assert.Equal(RandomSteamGameEventTypes.GamePickCompleted, publishedEvent.EventType);
        Assert.Equal(expectedProvider, publishedEvent.Payload.Provider);
        Assert.Equal(expectedAppId, publishedEvent.Payload.AppId);
        Assert.Equal(expectedGameName, publishedEvent.Payload.GameName);
        Assert.Equal(expectedUnplayedOnly, publishedEvent.Payload.UnplayedOnly);
        Assert.Equal(expectedOutcome, publishedEvent.Payload.Outcome);
        Assert.Equal(expectedSucceeded, publishedEvent.Payload.Succeeded);
        Assert.True(publishedEvent.Payload.DurationMilliseconds >= 0);
        Assert.True(publishedEvent.Payload.Timings.IdentifierResolutionMilliseconds >= 0);
        Assert.True(publishedEvent.Payload.Timings.LibraryLoadMilliseconds >= 0);
        Assert.True(publishedEvent.Payload.Timings.SelectionMilliseconds >= 0);
    }

    [Fact]
    public async Task GetRandomGameDetails_IncludesHashedVisitorIdInTelemetry()
    {
        var missionControl = new RecordingMissionControlClient();

        var controller = CreateController(
            missionControlClient: missionControl,
            remoteIpAddress: "192.0.2.42");

        var result = await controller.GetRandomGameDetails(
            "steam",
            76561197960287930L,
            vanityUrl: null);

        Assert.IsType<OkObjectResult>(result);

        var published = Assert.Single(missionControl.PublishedEvents);

        Assert.Equal(
            "hashed-192.0.2.42",
            published.Payload.VisitorId);
    }

    private sealed class FakeGameProvider : IGameProvider, ISteamDeckCompatibilityProvider
    {
        private readonly OwnedGamesResponse _library;
        private readonly ErrorOr<GameDetails> _randomGameResult;
        private readonly ErrorOr<long> _resolveIdentifierResult;
        private readonly OwnedGamesCacheInfo _cacheInfo;
        private readonly int _eligibleGameCount;
        private readonly int _libraryGameCount;
        private readonly IReadOnlyDictionary<int, SteamDeckCompatibilityCategory> _deckCompatibility;

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
            ErrorOr<long>? resolveIdentifierResult = null,
            OwnedGamesCacheInfo? cacheInfo = null,
            IReadOnlyDictionary<int, SteamDeckCompatibilityCategory>? deckCompatibility = null,
            int eligibleGameCount = 1,
            int libraryGameCount = 1)
        {
            _library = library ?? new OwnedGamesResponse(76561197960287930L, 0, []);
            _randomGameResult = randomGameResult ?? new GameDetails { Id = 1, Name = "Test" };
            _resolveIdentifierResult = resolveIdentifierResult ?? 76561197960287930L;
            _cacheInfo = cacheInfo ?? OwnedGamesCacheInfo.Unknown;
            _eligibleGameCount = eligibleGameCount;
            _libraryGameCount = libraryGameCount;
            _deckCompatibility = deckCompatibility ?? new Dictionary<int, SteamDeckCompatibilityCategory>();
        }

        public Task<IReadOnlyDictionary<int, SteamDeckCompatibilityCategory>>
            GetSteamDeckCompatibilityAsync(
                IEnumerable<int> appIds,
                CancellationToken ct = default)
        {
            return Task.FromResult(_deckCompatibility);
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

        public Task<RandomGamePickAttempt> GetRandomGamePickAsync(long userId, bool unplayedOnly = false)
        {
            GetRandomGameDetailsCallCount++;
            if (_randomGameResult.IsError)
            {
                return Task.FromResult(RandomGamePickAttempt.Failure(
                    _randomGameResult.Errors,
                    _cacheInfo,
                    _eligibleGameCount,
                    _libraryGameCount,
                    new GamePickTimings(0, 1, 1)));
            }

            return Task.FromResult(RandomGamePickAttempt.Success(
                _randomGameResult.Value,
                _cacheInfo,
                _eligibleGameCount,
                _libraryGameCount,
                new GamePickTimings(0, 1, 1)));
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

        public Task<AppStatsResponse> RecordHitAsync(
            string ip,
            string? userAgent = null) =>
            Task.FromResult(new AppStatsResponse(0, 0, 0));

        public Task<AppStatsResponse> GetStatsAsync()
            => Task.FromResult(new AppStatsResponse(0, 0, 0));

        public Task IncrementLibrariesExportedAsync()
        {
            return Task.CompletedTask;
        }

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

    private sealed class FakeLibraryExportCooldownTracker
    : ILibraryExportCooldownTracker
    {
        public TimeSpan? RetryAfter { get; set; }

        public int MarkSucceededCallCount { get; private set; }

        public string? LastPartitionKey { get; private set; }

        public TimeSpan? GetRetryAfter(string partitionKey)
        {
            LastPartitionKey = partitionKey;
            return RetryAfter;
        }

        public void MarkSucceeded(string partitionKey)
        {
            LastPartitionKey = partitionKey;
            MarkSucceededCallCount++;
        }
    }

    private sealed class RecordingMissionControlClient : IMissionControlClient
    {
        public List<PublishedEventRecord> PublishedEvents { get; } = [];
        public List<PublishedLibraryExportEventRecord> LibraryExportEvents { get; } = [];
        public List<PublishedLibraryExportRejectedEventRecord> LibraryExportRejectedEvents { get; } = [];

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
                case LibraryExportRejectedEvent libraryExportRejected:
                    LibraryExportRejectedEvents.Add(
                        new PublishedLibraryExportRejectedEventRecord(
                            eventType,
                            libraryExportRejected,
                            occurredAt,
                            correlationId ?? string.Empty));
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

    private sealed class StubVisitorIdProvider : IVisitorIdProvider
    {
        public string GetVisitorId(string ipAddress)
            => $"hashed-{ipAddress}";
    }

    private sealed record PublishedEventRecord(
        string EventType,
        GamePickCompletedEvent Payload,
        DateTimeOffset OccurredAt,
        string CorrelationId);

    private sealed record PublishedLibraryExportEventRecord(
        string EventType,
        LibraryExportCompletedEvent Payload,
        DateTimeOffset OccurredAt,
        string CorrelationId);

    private sealed record PublishedLibraryExportRejectedEventRecord(
        string EventType,
        LibraryExportRejectedEvent Payload,
        DateTimeOffset OccurredAt,
        string CorrelationId);
}
