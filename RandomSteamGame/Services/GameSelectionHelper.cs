namespace RandomSteamGame.Services;

public static class GameSelectionHelper
{
    public static HashSet<int> ParseExcludedGameIds(string? cookieValue) =>
        string.IsNullOrWhiteSpace(cookieValue) ? [] : cookieValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var appId) ? appId : (int?)null)
            .Where(appId => appId.HasValue)
            .Select(appId => appId!.Value)
            .ToHashSet();

    public static List<int> GetSelectableGameIds<TGame>(
        IEnumerable<TGame> ownedGames,
        ISet<int> excludedAppIds,
        Func<TGame, int> appIdSelector,
        Func<TGame, bool>? eligibilityPredicate = null,
        Random? random = null)
    {
        var selectableAppIds = ownedGames
            .Where(game => eligibilityPredicate?.Invoke(game) ?? true)
            .Select(appIdSelector)
            .Where(appId => !excludedAppIds.Contains(appId))
            .ToList();

        Shuffle(selectableAppIds, random ?? Random.Shared);
        return selectableAppIds;
    }

    private static void Shuffle<T>(IList<T> items, Random random)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
