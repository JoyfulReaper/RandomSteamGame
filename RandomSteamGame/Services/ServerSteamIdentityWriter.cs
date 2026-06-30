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

    public ServerSteamIdentityWriter(IHttpContextAccessor http)
    {
        _http = http;
    }

    public Task SetIdentityAsync(SteamIdentity identity)
    {
        var options = new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(365),
            SameSite = SameSiteMode.Lax,
            HttpOnly = false, // Browser JS needs to read it
            Secure = true
        };

        _http.HttpContext?.Response.Cookies.Append("SteamId", identity.SteamId ?? "0", options);
        _http.HttpContext?.Response.Cookies.Append("VanityUrl", identity.VanityUrl ?? "", options);
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        _http.HttpContext!.Response.Cookies.Delete("SteamId", new CookieOptions
        {
            Path = "/"
        });

        _http.HttpContext!.Response.Cookies.Delete("VanityUrl", new CookieOptions
        {
            Path = "/"
        });
        return Task.CompletedTask;
    }
}