namespace RandomSteamGame.Services;

public static class GameSelectionHelper
{
    public static List<int> GetSelectableGameIds(
        IEnumerable<int> ownedAppIds,
        ISet<int> excludedAppIds,
        Random? random = null)
    {
        var selectableAppIds = ownedAppIds
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
