namespace RandomSteamGame.Options;

public sealed class HostingOptions
{
    public const string SectionName = "Hosting";

    public string? Message { get; set; } = "Proudly hosted with";
    public string? ProviderName { get; set; } = "GreenCloud VPS";
    public string? Url { get; set; }
    public string? AffiliateUrl { get; set; } = "https://greencloudvps.com/billing/aff.php?aff=10295";
    public bool ShowAffiliateDisclosure { get; set; } = true;

    // An affiliate URL takes precedence; clearing both URLs leaves the provider as plain text.
    public string? LinkUrl => !string.IsNullOrWhiteSpace(AffiliateUrl) ? AffiliateUrl : Url;
    public bool HasAffiliateLink =>
        !string.IsNullOrWhiteSpace(ProviderName) && !string.IsNullOrWhiteSpace(AffiliateUrl);
}
