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
    public async Task GetRandomGame_InvalidSteamId_ReturnsValidationProblem(long steamId)
    {
        var controller = CreateController();

        var result = await controller.GetRandomGame("steam", steamId, vanityUrl: null);

        AssertValidationProblem(result, "Steam.InvalidSteamId");
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("has/slash")]
    [InlineData("has.dot")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task ResolveVanity_InvalidVanityUrl_ReturnsValidationProblem(string vanityUrl)
    {
        var controller = CreateController();

        var result = await controller.ResolveVanity("steam", vanityUrl);

        AssertValidationProblem(result, "Steam.InvalidVanityUrl");
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("has/slash")]
    public async Task GetRandomGame_InvalidVanityUrl_ReturnsValidationProblem(string vanityUrl)
    {
        var controller = CreateController();

        var result = await controller.GetRandomGame("steam", userId: null, vanityUrl);

        AssertValidationProblem(result, "Steam.InvalidVanityUrl");
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

    private static GameController CreateController()
    {
        var controller = new GameController(
            new GameProviderFactory([new FakeGameProvider()]),
            new FakeOwnedGamesCacheResetTracker(),
            new FakeAppStatsService(),
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
        public string ProviderKey => "steam";

        public Task<ErrorOr<OwnedGamesResponse>> GetOwnedGamesAsync(long userId)
            => Task.FromResult<ErrorOr<OwnedGamesResponse>>(new OwnedGamesResponse(userId, 0, []));

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
