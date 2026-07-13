namespace RandomSteamGame.Events;

public sealed record GamePickCompletedEvent(
    string Provider,
    int? AppId,
    string? GameName,
    bool UnplayedOnly,
    long DurationMilliseconds,
    string CacheStatus,
    long? CacheAgeSeconds,
    int? EligibleGameCount,
    string? LibrarySizeBucket,
    GamePickTimings Timings,
    string? CommitSha,
    string Outcome,
    bool Succeeded);

public sealed record GamePickTimings(
    long IdentifierResolutionMilliseconds,
    long LibraryLoadMilliseconds,
    long SelectionMilliseconds);
