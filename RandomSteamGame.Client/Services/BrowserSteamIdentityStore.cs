/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.JSInterop;
using RandomSteamGame.Shared.Contracts;
using RandomSteamGame.Shared.Interfaces;
using System.Globalization;

namespace RandomSteamGame.Client.Services;

public sealed class BrowserSteamIdentityStore :
    IBrowserSteamIdentityStore,
    ISteamIdentityReader,
    ISteamIdentityWriter,
    IAsyncDisposable
{
    private const string SteamIdCookieName = "SteamId";
    private const string VanityUrlCookieName = "VanityUrl";
    private const string OwnedGamesCacheResetAtCookieName = "OwnedGamesCacheResetAt";
    private const string ExcludedGameIdsCookieName = "ExcludedGameIds";
    private const int CookieLifetimeDays = 365;
    private const int ExcludedGameIdsCookieLifetimeDays = 365;
    private const int OwnedGamesCacheResetCookieLifetimeDays = 2;

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

    public async ValueTask<IReadOnlyCollection<int>> GetExcludedGameIdsAsync()
    {
        var cookieModule = await GetCookieModuleAsync();
        var cookieValue = CleanInput(await cookieModule.InvokeAsync<string>("getCookie", ExcludedGameIdsCookieName));

        if (cookieValue is null)
        {
            return Array.Empty<int>();
        }

        return cookieValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var appId) ? appId : (int?)null)
            .Where(appId => appId.HasValue)
            .Select(appId => appId!.Value)
            .Distinct()
            .ToArray();
    }

    public async ValueTask SetExcludedGameIdsAsync(IEnumerable<int> appIds)
    {
        var distinctAppIds = appIds
            .Where(appId => appId > 0)
            .Distinct()
            .ToArray();

        var cookieModule = await GetCookieModuleAsync();

        if (distinctAppIds.Length == 0)
        {
            await cookieModule.InvokeVoidAsync("deleteCookie", ExcludedGameIdsCookieName);
            return;
        }

        await cookieModule.InvokeVoidAsync(
            "setCookie",
            ExcludedGameIdsCookieName,
            string.Join(",", distinctAppIds),
            ExcludedGameIdsCookieLifetimeDays);
    }

    public async ValueTask ClearExcludedGameIdsAsync()
    {
        var cookieModule = await GetCookieModuleAsync();
        await cookieModule.InvokeVoidAsync("deleteCookie", ExcludedGameIdsCookieName);
    }

    public async ValueTask<DateTimeOffset?> GetOwnedGamesCacheResetAtAsync()
    {
        var cookieModule = await GetCookieModuleAsync();
        var cookieValue = CleanInput(await cookieModule.InvokeAsync<string>("getCookie", OwnedGamesCacheResetAtCookieName));

        if (cookieValue is null)
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            cookieValue,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var resetAt)
            ? resetAt
            : null;
    }

    public async ValueTask SetOwnedGamesCacheResetAtAsync(DateTimeOffset resetAt)
    {
        var cookieModule = await GetCookieModuleAsync();
        await cookieModule.InvokeVoidAsync(
            "setCookie",
            OwnedGamesCacheResetAtCookieName,
            resetAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            OwnedGamesCacheResetCookieLifetimeDays);
    }

    public async ValueTask ClearOwnedGamesCacheResetAtAsync()
    {
        var cookieModule = await GetCookieModuleAsync();
        await cookieModule.InvokeVoidAsync("deleteCookie", OwnedGamesCacheResetAtCookieName);
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
