namespace SteamApiClient.Services;

public enum OwnedGamesCacheStatus
{
    Hit,
    Miss,
    Refreshed,
    Stale,
    Bypassed,
    Unknown
}
