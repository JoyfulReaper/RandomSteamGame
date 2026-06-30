namespace RandomSteamGame.Shared.Contracts;

public sealed record AppStatsResponse(
    long TotalHits,
    long UniqueVisitors);
