namespace RandomSteamGame.Services;

public static class GamePickOutcome
{
    public const string Served = "served";
    public const string UnsupportedProvider = "unsupported-provider";
    public const string InvalidIdentifier = "invalid-identifier";
    public const string IdentifierResolutionFailed = "identifier-resolution-failed";
    public const string PrivateProfile = "private-profile";
    public const string LibraryLoadFailed = "library-load-failed";
    public const string EmptyLibrary = "empty-library";
    public const string NoEligibleGames = "no-eligible-games";
    public const string RateLimited = "rate-limited";
    public const string SelectionFailed = "selection-failed";
}
