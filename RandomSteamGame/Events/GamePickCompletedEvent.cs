namespace RandomSteamGame.Events;

public sealed record GamePickCompletedEvent(
    string Provider,
    int? AppId,
    bool UnplayedOnly,
    long DurationMilliseconds,
    string Outcome,
    bool Succeeded);
