namespace RandomSteamGame.Events;

public sealed record GamePickCompletedEvent(
    string Provider,
    int? AppId,
    string? GameName,
    bool UnplayedOnly,
    long DurationMilliseconds,
    string Outcome,
    bool Succeeded);