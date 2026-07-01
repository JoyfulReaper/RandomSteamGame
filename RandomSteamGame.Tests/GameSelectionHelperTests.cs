using RandomSteamGame.Services;

namespace RandomSteamGame.Tests;

public class GameSelectionHelperTests
{
    [Fact]
    public void GetSelectableGames_RemovesExcludedGames()
    {
        var games = CreateGames();
        var excluded = new HashSet<int> { 2, 4 };

        var result = GameSelectionHelper.GetSelectableGameIds(games, excluded, game => game.AppId, random: new Random(42));

        Assert.Equal(new[] { 1, 3, 5 }, result.OrderBy(appId => appId));
        Assert.DoesNotContain(result, appId => excluded.Contains(appId));
    }

    [Fact]
    public void GetSelectableGames_ReturnsEmpty_WhenAllGamesExcluded()
    {
        var games = CreateGames();
        var excluded = new HashSet<int> { 1, 2, 3, 4, 5 };

        var result = GameSelectionHelper.GetSelectableGameIds(games, excluded, game => game.AppId, random: new Random(42));

        Assert.Empty(result);
    }

    [Fact]
    public void GetSelectableGames_ReturnsEachRemainingGameExactlyOnce()
    {
        var games = CreateGames(12);
        var excluded = new HashSet<int> { 2, 6, 9 };

        var result = GameSelectionHelper.GetSelectableGameIds(games, excluded, game => game.AppId, random: new Random(7));
        var expectedIds = games
            .Select(game => game.AppId)
            .Where(appId => !excluded.Contains(appId))
            .OrderBy(appId => appId)
            .ToArray();

        Assert.Equal(expectedIds.Length, result.Count);
        Assert.Equal(expectedIds, result.OrderBy(appId => appId));
        Assert.Equal(result.Count, result.Distinct().Count());
    }

    [Fact]
    public void GetSelectableGames_CanFilterToUnplayedGames()
    {
        var games = new[]
        {
            new TestGame(1, true),
            new TestGame(2, false),
            new TestGame(3, true),
            new TestGame(4, false)
        };

        var result = GameSelectionHelper.GetSelectableGameIds(
            games,
            excludedAppIds: new HashSet<int>(),
            appIdSelector: game => game.AppId,
            eligibilityPredicate: game => game.IsUnplayed,
            random: new Random(3));

        Assert.Equal(new[] { 1, 3 }, result.OrderBy(appId => appId));
    }

    private static List<TestGame> CreateGames(int count = 5)
    {
        return Enumerable.Range(1, count)
            .Select(appId => new TestGame(appId, true))
            .ToList();
    }

    private sealed record TestGame(int AppId, bool IsUnplayed);
}
