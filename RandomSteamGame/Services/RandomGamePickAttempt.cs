using ErrorOr;
using RandomSteamGame.Events;
using RandomSteamGame.Shared.Contracts;
using SteamApiClient.Services;

namespace RandomSteamGame.Services;

public sealed record RandomGamePickAttempt(
    GameDetails? Game,
    IReadOnlyList<Error> Errors,
    OwnedGamesCacheInfo Cache,
    int? EligibleGameCount,
    int? LibraryGameCount,
    GamePickTimings Timings)
{
    public bool Succeeded => Game is not null && Errors.Count == 0;

    public static RandomGamePickAttempt Success(
        GameDetails game,
        OwnedGamesCacheInfo cache,
        int eligibleGameCount,
        int libraryGameCount,
        GamePickTimings timings)
        => new(game, [], cache, eligibleGameCount, libraryGameCount, timings);

    public static RandomGamePickAttempt Failure(
        IReadOnlyList<Error> errors,
        OwnedGamesCacheInfo? cache = null,
        int? eligibleGameCount = null,
        int? libraryGameCount = null,
        GamePickTimings? timings = null)
        => new(
            null,
            errors,
            cache ?? OwnedGamesCacheInfo.Unknown,
            eligibleGameCount,
            libraryGameCount,
            timings ?? new GamePickTimings(0, 0, 0));
}
