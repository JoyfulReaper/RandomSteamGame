using Microsoft.Extensions.Options;
using RandomSteamGame.Options;

namespace RandomSteamGame.Services;

public sealed class CanonicalUrlService
{
    private readonly string _canonicalOrigin;

    public CanonicalUrlService(IOptions<ApplicationOptions> options)
    {
        var settings = options.Value;
        if (!Uri.TryCreate(settings.CanonicalOrigin, UriKind.Absolute, out var origin) ||
            !string.Equals(origin.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            origin.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(origin.Query) ||
            !string.IsNullOrEmpty(origin.Fragment) ||
            !string.IsNullOrEmpty(origin.UserInfo))
        {
            throw new InvalidOperationException(
                $"Application:{nameof(ApplicationOptions.CanonicalOrigin)} must be an HTTPS origin without a path, query, or fragment.");
        }

        if (string.IsNullOrWhiteSpace(settings.BetaHost))
        {
            throw new InvalidOperationException(
                $"Application:{nameof(ApplicationOptions.BetaHost)} must be configured.");
        }

        _canonicalOrigin = origin.GetLeftPart(UriPartial.Authority);
        BetaHost = settings.BetaHost.Trim().TrimEnd('.');
    }

    public string BetaHost { get; }

    public string GetCanonicalUrl(string path = "/")
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return _canonicalOrigin;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Canonical paths must be relative to the configured origin.", nameof(path));
        }

        var origin = new Uri($"{_canonicalOrigin}/", UriKind.Absolute);
        return new Uri(origin, path.TrimStart('/')).AbsoluteUri;
    }

    public bool IsBetaHost(string? host)
    {
        return string.Equals(
            host?.TrimEnd('.'),
            BetaHost,
            StringComparison.OrdinalIgnoreCase);
    }
}
