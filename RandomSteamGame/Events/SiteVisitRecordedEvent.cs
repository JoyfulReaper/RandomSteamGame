namespace RandomSteamGame.Events;

public sealed record SiteVisitRecordedEvent(
    long TotalHits,
    long UniqueVisitors,
    long DurationMilliseconds);