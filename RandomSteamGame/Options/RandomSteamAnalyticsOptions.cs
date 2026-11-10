
namespace RandomSteamGame.Options;

public sealed class RandomSteamAnalyticsOptions
{
    public const string SectionName = "RandomSteamAnalytics";

    public bool Enabled { get; set; }
    public string? ArchiveBaseUrl { get; set; }
    public string? ApiKey { get; set; }
}
