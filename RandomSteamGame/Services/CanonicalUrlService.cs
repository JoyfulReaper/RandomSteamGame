using Microsoft.Extensions.Options;
using RandomSteamGame.Options;
using System.Diagnostics.CodeAnalysis;

namespace RandomSteamGame.Services;

public sealed class CanonicalUrlService
{
    internal const string OriginValidationMessage =
        "Application:CanonicalOrigin must be an origin without credentials, path, query, or fragment. Public requires HTTPS; AltNet requires an explicit HTTP or HTTPS origin.";

    private readonly string _canonicalOrigin;

    public CanonicalUrlService(IOptions<ApplicationOptions> options)
    {
        var settings = options.Value;
        if (!TryGetOrigin(settings, out var origin))
        {
            throw new InvalidOperationException(OriginValidationMessage);
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

    internal static bool TryGetOrigin(ApplicationOptions settings, [NotNullWhen(true)] out Uri? origin)
    {
        var configuredOrigin = settings.CanonicalOrigin ??
            (settings.NetworkMode == NetworkMode.Public ? "https://randomsteam.kgivler.com" : null);

        return Uri.TryCreate(configuredOrigin, UriKind.Absolute, out origin) &&
            (origin.Scheme == Uri.UriSchemeHttps ||
                (settings.NetworkMode == NetworkMode.AltNet && origin.Scheme == Uri.UriSchemeHttp)) &&
            !string.IsNullOrEmpty(origin.Host) &&
            origin.AbsolutePath == "/" &&
            string.IsNullOrEmpty(origin.Query) &&
            string.IsNullOrEmpty(origin.Fragment) &&
            string.IsNullOrEmpty(origin.UserInfo);
    }

    public string GetCanonicalUrl(string path = "/")
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return _canonicalOrigin;
        }

        var relativePath = path.TrimStart('/');
        if (!Uri.TryCreate(relativePath, UriKind.Relative, out var relativeUri))
        {
            throw new ArgumentException("Canonical paths must be relative to the configured origin.", nameof(path));
        }

        var origin = new Uri($"{_canonicalOrigin}/", UriKind.Absolute);
        return new Uri(origin, relativeUri).AbsoluteUri;
    }

    public bool IsBetaHost(string? host)
    {
        return string.Equals(
            host?.TrimEnd('.'),
            BetaHost,
            StringComparison.OrdinalIgnoreCase);
    }
}
