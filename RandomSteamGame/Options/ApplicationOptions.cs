namespace RandomSteamGame.Options;

public sealed class ApplicationOptions
{
    public const string SectionName = "Application";

    public NetworkMode NetworkMode { get; set; } = NetworkMode.Public;

    public string? NetworkName { get; set; }

    // Null uses the deployment mode's default; explicit values override it.
    public bool? AllowRemoteBrowserAssets { get; set; }

    public bool? EnableBetaProbe { get; set; }

    public bool RemoteBrowserAssetsAllowed =>
        AllowRemoteBrowserAssets ?? NetworkMode == NetworkMode.Public;

    public bool BetaProbeEnabled =>
        EnableBetaProbe ?? NetworkMode == NetworkMode.Public;

    public string NetworkDisplayName =>
        string.IsNullOrWhiteSpace(NetworkName) ? NetworkMode.ToString() : NetworkName.Trim();

    public string CanonicalOrigin { get; set; } = "https://randomsteam.kgivler.com";

    public string BetaHost { get; set; } = "randombeta.kgivler.com";

    public string? CommitSha { get; set; }

    public string? DeploymentType { get; set; }
}
