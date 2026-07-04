using ErrorOr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using RandomSteamGame.Controllers;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using System.Reflection;
using System.Text;

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
    [InlineData(0)]
    [InlineData(123456789)]
    public async Task GetRandomGame_InvalidSteamId_ReturnsValidationProblem(long steamId)
    {
        var controller = CreateController();

        var result = await controller.GetRandomGame("steam", steamId, vanityUrl: null);

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
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("https://steamcommunity.com/profiles/76561197960287930/")]
    public async Task GetRandomGame_InvalidVanityUrl_ReturnsValidationProblem(string vanityUrl)
    {
        var controller = CreateController();

        var result = await controller.GetRandomGame("steam", userId: null, vanityUrl);

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
    public void HitController_UsesRateLimitingPolicy()
    {
        var attribute = typeof(HitController).GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("steam_api_limiter", attribute.PolicyName);
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

    private static GameController CreateController(FakeGameProvider? provider = null)
    {
        var controller = new GameController(
            new GameProviderFactory([provider ?? new FakeGameProvider()]),
            new FakeOwnedGamesCacheResetTracker(),
            new FakeAppStatsService(),
            new SteamLibraryExportService(),
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

    private sealed class FakeGameProvider : IGameProvider
    {
        private readonly OwnedGamesResponse _library;

        public FakeGameProvider()
            : this(new OwnedGamesResponse(76561197960287930L, 0, []))
        {
        }

        public FakeGameProvider(OwnedGamesResponse library)
        {
            _library = library;
        }

        public string ProviderKey => "steam";

        public Task<ErrorOr<OwnedGamesResponse>> GetOwnedGamesAsync(long userId)
            => Task.FromResult<ErrorOr<OwnedGamesResponse>>(_library with { SteamId = userId });

        public Task<ErrorOr<RandomGameResponse>> GetRandomGameAsync(long userId, bool unplayedOnly = false)
            => Task.FromResult<ErrorOr<RandomGameResponse>>(CreateRandomGameResponse(userId));

        public Task<ErrorOr<GameDetails>> GetRandomGameDetailsAsync(long userId, bool unplayedOnly = false)
            => Task.FromResult<ErrorOr<GameDetails>>(new GameDetails { Id = 1, Name = "Test" });

        public Task<ErrorOr<long>> ResolveIdentifierAsync(string identifier)
            => Task.FromResult<ErrorOr<long>>(76561197960287930L);

        public Task InvalidateOwnedGamesCacheAsync(long userId)
            => Task.CompletedTask;

        private static RandomGameResponse CreateRandomGameResponse(long userId)
            => new(
                userId,
                1,
                new SteamAppInformation("Test", 1, false, "", "", "", "", ""),
                0,
                0,
                0);
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
        public Task<AppStatsResponse> RecordHitAsync(string ip)
            => Task.FromResult(new AppStatsResponse(0, 0, 0));

        public Task<AppStatsResponse> GetStatsAsync()
            => Task.FromResult(new AppStatsResponse(0, 0, 0));

        public Task IncrementRandomGamesGeneratedAsync()
            => Task.CompletedTask;
    }
}
