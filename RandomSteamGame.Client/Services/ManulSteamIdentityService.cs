/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */


using Mythetech.LocalStorage;
using RandomSteamGame.Shared.Interfaces;

namespace RandomSteamGame.Client.Services;

public class ManualSteamIdentityService : ISteamIdentityService
{
    private readonly ILocalStorageService _localStorage;

    public ManualSteamIdentityService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task<long?> GetSteamIdAsync()
    {
        return await _localStorage.GetItemAsync<long?>(LocalStorageKeys.SteamId);
    }

    public async Task SetIdentityAsync(long steamId, string? vanityUrl)
    {
        await _localStorage.SetItemAsync(LocalStorageKeys.SteamId, steamId);
        await _localStorage.SetItemAsStringAsync(LocalStorageKeys.VanityUrl, vanityUrl ?? "");
    }
    public async Task<string?> GetVanityUrlAsync()
    {
        var val = await _localStorage.GetItemAsync<string>(LocalStorageKeys.VanityUrl);
        return val ?? string.Empty;
    }

    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemsAsync(new List<string> {
            LocalStorageKeys.SteamId,
            LocalStorageKeys.VanityUrl
        });
    }
}