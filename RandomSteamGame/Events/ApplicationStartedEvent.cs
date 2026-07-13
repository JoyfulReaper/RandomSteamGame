namespace RandomSteamGame.Events;

public sealed record ApplicationStartedEvent(
    string Environment,
    string? CommitSha,
    string? DeploymentType,
    string FrameworkVersion);
