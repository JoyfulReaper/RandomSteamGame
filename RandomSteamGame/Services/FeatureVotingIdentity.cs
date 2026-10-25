using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace RandomSteamGame.Services;

public sealed class FeatureVotingIdentity(IDataProtectionProvider protection, DeploymentCookiePolicy cookiePolicy)
{
    internal const string CookieName = ".RandomSteamGame.FeatureVoter.v1";
    private readonly ITimeLimitedDataProtector _protector = protection
        .CreateProtector("RandomSteamGame.FeatureVoting.BrowserIdentity.v1").ToTimeLimitedDataProtector();

    public string? ReadHash(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var cookie) || cookie.Length > 1024)
            return null;
        try
        {
            var identifier = _protector.Unprotect(cookie, out _);
            if (identifier.Length != 64)
                return null;
            var bytes = Convert.FromHexString(identifier);
            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return null;
        }
    }

    public string GetOrCreateHash(HttpContext context)
    {
        var existing = ReadHash(context);
        if (existing is not null)
            return existing;

        var identifier = RandomNumberGenerator.GetBytes(32);
        var lifetime = TimeSpan.FromDays(365);
        context.Response.Cookies.Append(CookieName,
            _protector.Protect(Convert.ToHexString(identifier), lifetime), new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = cookiePolicy.UseSecureCookies,
                Path = "/api/features",
                MaxAge = lifetime,
                Expires = DateTimeOffset.UtcNow.Add(lifetime)
            });
        return Convert.ToHexStringLower(SHA256.HashData(identifier));
    }
}
