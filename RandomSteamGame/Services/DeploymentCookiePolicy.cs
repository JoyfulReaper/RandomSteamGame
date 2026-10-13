using Microsoft.Extensions.Options;
using RandomSteamGame.Options;

namespace RandomSteamGame.Services;

public sealed class DeploymentCookiePolicy(
    IOptions<ApplicationOptions> applicationOptions,
    CanonicalUrlService canonicalUrls)
{
    public bool IsAltNet => applicationOptions.Value.NetworkMode == NetworkMode.AltNet;
    public string ExternalScheme { get; } = new Uri(canonicalUrls.GetCanonicalUrl()).Scheme;
    public bool UseSecureCookies => !IsAltNet || ExternalScheme == Uri.UriSchemeHttps;

    public CookieSecurePolicy GetAntiforgerySecurePolicy(IHostEnvironment environment) =>
        !IsAltNet && environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : UseSecureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.None;
}
