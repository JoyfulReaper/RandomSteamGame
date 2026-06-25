/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */


using RandomSteamGame.Shared.Interfaces;

namespace RandomSteamGame.Services;

public class ServerSteamIdentityService : ISteamIdentityService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string SteamIdKey = "SteamId";
    private const string VanityUrlKey = "VanityUrl";

    public ServerSteamIdentityService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<long?> GetSteamIdAsync()
    {
        var cookieValue = _httpContextAccessor.HttpContext?.Request.Cookies[SteamIdKey];
        if (long.TryParse(cookieValue, out var steamId))
        {
            return Task.FromResult<long?>(steamId);
        }
        return Task.FromResult<long?>(null);
    }

    public async Task<string?> GetVanityUrlAsync()
    {
        var cookieValue = _httpContextAccessor.HttpContext?.Request.Cookies[VanityUrlKey];
        return await Task.FromResult(cookieValue ?? string.Empty);
    }

    public Task SetIdentityAsync(long steamId, string? vanityUrl)
    {
        var context = _httpContextAccessor.HttpContext;
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(30)
        };

        context?.Response.Cookies.Append(SteamIdKey, steamId.ToString(), cookieOptions);
        context?.Response.Cookies.Append(VanityUrlKey, vanityUrl ?? "", cookieOptions);

        return Task.CompletedTask;
    }

    public Task LogoutAsync()
    {
        var context = _httpContextAccessor.HttpContext;
        context?.Response.Cookies.Delete(SteamIdKey);
        context?.Response.Cookies.Delete(VanityUrlKey);

        return Task.CompletedTask;
    }
}