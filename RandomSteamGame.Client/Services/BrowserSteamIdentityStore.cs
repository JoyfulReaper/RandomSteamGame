/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.JSInterop;
using RandomSteamGame.Shared.Contracts;
using RandomSteamGame.Shared.Interfaces;

namespace RandomSteamGame.Client.Services;

public sealed class BrowserSteamIdentityStore :
    IBrowserSteamIdentityStore,
    ISteamIdentityReader,
    ISteamIdentityWriter,
    IAsyncDisposable
{
    private const string SteamIdCookieName = "SteamId";
    private const string VanityUrlCookieName = "VanityUrl";
    private const int CookieLifetimeDays = 365;

    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _cookieModule;

    public BrowserSteamIdentityStore(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async ValueTask<SteamIdentity> GetIdentityAsync()
    {
        var cookieModule = await GetCookieModuleAsync();
        var steamId = CleanInput(await cookieModule.InvokeAsync<string>("getCookie", SteamIdCookieName));
        var vanityUrl = steamId is null
            ? CleanInput(await cookieModule.InvokeAsync<string>("getCookie", VanityUrlCookieName))
            : null;

        return new SteamIdentity(steamId, vanityUrl);
    }

    async Task ISteamIdentityWriter.SetIdentityAsync(SteamIdentity identity)
        => await SetIdentityAsync(identity);

    async Task ISteamIdentityWriter.ClearAsync()
        => await ClearAsync();

    public async ValueTask SetIdentityAsync(SteamIdentity identity)
    {
        if (string.IsNullOrWhiteSpace(identity.SteamId))
        {
            return;
        }

        var cookieModule = await GetCookieModuleAsync();
        await cookieModule.InvokeVoidAsync("setCookie", SteamIdCookieName, identity.SteamId.Trim(), CookieLifetimeDays);
        await cookieModule.InvokeVoidAsync("deleteCookie", VanityUrlCookieName);
    }

    public async ValueTask ClearAsync()
    {
        var cookieModule = await GetCookieModuleAsync();
        await cookieModule.InvokeVoidAsync("deleteCookie", SteamIdCookieName);
        await cookieModule.InvokeVoidAsync("deleteCookie", VanityUrlCookieName);
    }

    public async ValueTask DisposeAsync()
    {
        if (_cookieModule is null)
        {
            return;
        }

        try
        {
            await _cookieModule.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // Server-side interactive circuits may already be closed during disposal.
        }
    }

    private async ValueTask<IJSObjectReference> GetCookieModuleAsync()
    {
        _cookieModule ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/cookieHelper.js");
        return _cookieModule;
    }

    private static string? CleanInput(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
