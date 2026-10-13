namespace RandomSteamGame.Options;

public sealed class IngressOptions
{
    public const string SectionName = "Ingress";

    // Null preserves Public loopback proxy support; AltNet ignores forwarding by default.
    public bool? EnableForwardedHeaders { get; set; }
    public bool? EnableCloudflareVisitorHeader { get; set; }
    // Apply the loopback fallback after binding so configured arrays replace it rather than append to it.
    public string[]? TrustedProxies { get; set; }
}
