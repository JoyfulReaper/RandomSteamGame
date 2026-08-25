namespace RandomSteamGame.Events;

public sealed record SiteVisitRecordedEvent(
    string? VisitorId,
    bool IsUniqueVisitor,
    long TotalHits,
    long UniqueVisitors,
    long DurationMilliseconds);