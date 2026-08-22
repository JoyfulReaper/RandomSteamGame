namespace RandomSteamGame.Events;

public sealed record SiteVisitRecordedEvent(
    string VisitorId,
    long TotalHits,
    long UniqueVisitors,
    long DurationMilliseconds);