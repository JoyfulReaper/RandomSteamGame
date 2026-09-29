namespace RandomSteamGame.Events;

public sealed record SiteVisitRecordedEvent(
    string? VisitorId,
    string? UserAgent,
    string IngressNetwork,
    bool IsUniqueVisitor,
    long TotalHits,
    long UniqueVisitors,
    long DurationMilliseconds);