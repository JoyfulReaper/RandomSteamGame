/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.JSInterop;
using RandomSteamGame.Shared.Interfaces;

namespace RandomSteamGame.Client.Services;

public class BrowserSteamIdentityReader : ISteamIdentityReader
{
    private readonly IJSRuntime _js;

    public BrowserSteamIdentityReader(IJSRuntime js)
    {
        _js = js;
    }

    public async ValueTask<long?> GetSteamIdAsync()
    {
        var cookieStr = await _js.InvokeAsync<string>("eval", "document.cookie");
        var idString = ExtractCookie(cookieStr, "SteamId");
        return long.TryParse(idString, out var id) ? id : null;
    }

    public async ValueTask<string?> GetVanityUrlAsync()
    {
        var cookieStr = await _js.InvokeAsync<string>("eval", "document.cookie");
        return ExtractCookie(cookieStr, "VanityUrl");
    }

    private string? ExtractCookie(string cookieString, string cookieName)
    {
        if (string.IsNullOrEmpty(cookieString)) return null;

        var cookies = cookieString.Split(';');
        foreach (var cookie in cookies)
        {
            var parts = cookie.Trim().Split('=');
            if (parts.Length == 2 && parts[0] == cookieName)
            {
                return parts[1];
            }
        }
        return null;
    }
}