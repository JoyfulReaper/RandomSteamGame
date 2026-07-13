namespace RandomSteamGame.Options;

public sealed class ApplicationOptions
{
    public const string SectionName = "Application";

    public string? CommitSha { get; set; }

    public string? DeploymentType { get; set; }
}
