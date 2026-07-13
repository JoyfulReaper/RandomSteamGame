namespace SteamApiClient.Services;

public sealed record OwnedGamesCacheInfo(
    OwnedGamesCacheStatus Status,
    long? AgeSeconds)
{
    public static OwnedGamesCacheInfo Unknown { get; } =
        new(OwnedGamesCacheStatus.Unknown, null);

    public string StatusName => Status switch
    {
        OwnedGamesCacheStatus.Hit => "hit",
        OwnedGamesCacheStatus.Miss => "miss",
        OwnedGamesCacheStatus.Refreshed => "refreshed",
        OwnedGamesCacheStatus.Stale => "stale",
        OwnedGamesCacheStatus.Bypassed => "bypassed",
        _ => "unknown"
    };
}
