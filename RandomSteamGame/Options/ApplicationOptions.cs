namespace RandomSteamGame.Options;

public sealed class ApplicationOptions
{
    public const string SectionName = "Application";

    public string CanonicalOrigin { get; set; } = "https://randomsteam.kgivler.com";

    public string BetaHost { get; set; } = "randombeta.kgivler.com";

    public string? CommitSha { get; set; }

    public string? DeploymentType { get; set; }
}
