/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */


using RandomSteamGame.Shared.Contracts;
using RandomSteamGame.Shared.Interfaces;

namespace RandomSteamGame.Services;

public class ServerSteamIdentityWriter : ISteamIdentityWriter
{
    private readonly IHttpContextAccessor _http;
    private readonly DeploymentCookiePolicy _cookiePolicy;

    public ServerSteamIdentityWriter(IHttpContextAccessor http, DeploymentCookiePolicy cookiePolicy)
    {
        _http = http;
        _cookiePolicy = cookiePolicy;
    }

    public Task SetIdentityAsync(SteamIdentity identity)
    {
        var response = _http.HttpContext?.Response;
        if (response is null || response.HasStarted || string.IsNullOrWhiteSpace(identity.SteamId))
        {
            return Task.CompletedTask;
        }

        var options = CreateCookieOptions();

        response.Cookies.Append(SteamIdentityCookies.SteamId, identity.SteamId, options);
        response.Cookies.Append(SteamIdentityCookies.UnplayedOnly, identity.UnplayedOnly.ToString().ToLowerInvariant(), options);
        response.Cookies.Delete(SteamIdentityCookies.VanityUrl, CreateDeleteCookieOptions());

        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        var response = _http.HttpContext?.Response;
        if (response is null || response.HasStarted)
        {
            return Task.CompletedTask;
        }

        var options = CreateDeleteCookieOptions();

        response.Cookies.Delete(SteamIdentityCookies.SteamId, options);
        response.Cookies.Delete(SteamIdentityCookies.VanityUrl, options);
        response.Cookies.Delete(SteamIdentityCookies.UnplayedOnly, options);

        return Task.CompletedTask;
    }

    private CookieOptions CreateCookieOptions() => new()
    {
        Expires = DateTimeOffset.UtcNow.AddDays(365),
        SameSite = SameSiteMode.Lax,
        HttpOnly = false,
        Secure = _cookiePolicy.UseSecureCookies,
        Path = "/"
    };

    private CookieOptions CreateDeleteCookieOptions() => new()
    {
        SameSite = SameSiteMode.Lax,
        Secure = _cookiePolicy.UseSecureCookies,
        Path = "/"
    };
}
