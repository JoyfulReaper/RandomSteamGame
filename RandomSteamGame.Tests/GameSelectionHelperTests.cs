using RandomSteamGame.Services;

namespace RandomSteamGame.Tests;

public class GameSelectionHelperTests
{
    [Fact]
    public void GetSelectableGames_RemovesExcludedGames()
    {
        var appIds = CreateAppIds();
        var excluded = new HashSet<int> { 2, 4 };

        var result = GameSelectionHelper.GetSelectableGameIds(appIds, excluded, new Random(42));

        Assert.Equal(new[] { 1, 3, 5 }, result.OrderBy(appId => appId));
        Assert.DoesNotContain(result, appId => excluded.Contains(appId));
    }

    [Fact]
    public void GetSelectableGames_ReturnsEmpty_WhenAllGamesExcluded()
    {
        var appIds = CreateAppIds();
        var excluded = new HashSet<int> { 1, 2, 3, 4, 5 };

        var result = GameSelectionHelper.GetSelectableGameIds(appIds, excluded, new Random(42));

        Assert.Empty(result);
    }

    [Fact]
    public void GetSelectableGames_ReturnsEachRemainingGameExactlyOnce()
    {
        var appIds = CreateAppIds(12);
        var excluded = new HashSet<int> { 2, 6, 9 };

        var result = GameSelectionHelper.GetSelectableGameIds(appIds, excluded, new Random(7));
        var expectedIds = appIds
            .Where(appId => !excluded.Contains(appId))
            .OrderBy(appId => appId)
            .ToArray();

        Assert.Equal(expectedIds.Length, result.Count);
        Assert.Equal(expectedIds, result.OrderBy(appId => appId));
        Assert.Equal(result.Count, result.Distinct().Count());
    }

    private static List<int> CreateAppIds(int count = 5)
    {
        return Enumerable.Range(1, count)
            .ToList();
    }
}
