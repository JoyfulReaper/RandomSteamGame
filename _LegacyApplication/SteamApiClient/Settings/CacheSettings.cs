namespace SteamApiClient.Settings;

public class CacheSettings
{
    public CachePolicy OwnedGames { get; set; } = default!;
    public CachePolicy AppDetails { get; set; } = default!;
    public CachePolicy VanitySuccess { get; set; } = default!;
    public CachePolicy VanityNotFound { get; set; } = default!;
}

public class CachePolicy
{
    public int AbsoluteMinutes { get; set; }

    public TimeSpan Duration =>
        TimeSpan.FromMinutes(AbsoluteMinutes);
}