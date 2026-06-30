/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */


using RandomSteamGame.Shared.Interfaces;

namespace RandomSteamGame.Services;

public class ServerSteamIdentityWriter : ISteamIdentityWriter
{
    private readonly IHttpContextAccessor _http;

    public ServerSteamIdentityWriter(IHttpContextAccessor http)
    {
        _http = http;
    }

    public Task SetIdentityAsync(string steamId, string? vanityUrl)
    {
        _http.HttpContext?.Response.Cookies.Append("SteamId", steamId.ToString());
        _http.HttpContext?.Response.Cookies.Append("VanityUrl", vanityUrl ?? "");
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        _http.HttpContext?.Response.Cookies.Delete("SteamId");
        _http.HttpContext?.Response.Cookies.Delete("VanityUrl");
        return Task.CompletedTask;
    }
}