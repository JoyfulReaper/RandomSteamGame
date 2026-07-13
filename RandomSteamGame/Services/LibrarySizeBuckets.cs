namespace RandomSteamGame.Services;

public static class LibrarySizeBuckets
{
    public static string FromCount(int gameCount)
        => gameCount switch
        {
            <= 0 => "0",
            <= 24 => "1-24",
            <= 99 => "25-99",
            <= 249 => "100-249",
            <= 499 => "250-499",
            <= 999 => "500-999",
            _ => "1000+"
        };
}
