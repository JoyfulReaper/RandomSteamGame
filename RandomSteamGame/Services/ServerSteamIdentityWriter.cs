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
        var response = _http.HttpContext?.Response;
        if (response is null || response.HasStarted)
        {
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(identity.SteamId))
        {
            return Task.CompletedTask;
        }

        var options = new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(365),
            SameSite = SameSiteMode.Lax,
            HttpOnly = false, // Browser JS needs to read it
            Secure = true
        };

        response.Cookies.Append("SteamId", identity.SteamId, options);
        response.Cookies.Delete("VanityUrl", new CookieOptions { Path = "/" });
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        var response = _http.HttpContext?.Response;
        if (response is null || response.HasStarted)
        {
            return Task.CompletedTask;
        }

        response.Cookies.Delete("SteamId", new CookieOptions
        {
            Path = "/"
        });

        response.Cookies.Delete("VanityUrl", new CookieOptions
        {
            Path = "/"
        });
        return Task.CompletedTask;
    }
}
