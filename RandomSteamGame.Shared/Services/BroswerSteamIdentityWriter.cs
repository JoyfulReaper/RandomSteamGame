/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Mythetech.LocalStorage;
using RandomSteamGame.Shared.Contracts;
using RandomSteamGame.Shared.Interfaces;

namespace RandomSteamGame.Shared.Services;

public class BrowserSteamIdentityWriter : ISteamIdentityWriter
{
    private readonly ILocalStorageService _localStorage;

    public BrowserSteamIdentityWriter(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public async Task SetIdentityAsync(long steamId, string? vanityUrl)
    {
        await _localStorage.SetItemAsync(LocalStorageKeys.SteamId, steamId);
        await _localStorage.SetItemAsync(LocalStorageKeys.VanityUrl, vanityUrl);
    }

    public async Task ClearAsync()
    {
        await _localStorage.RemoveItemAsync(LocalStorageKeys.SteamId);
        await _localStorage.RemoveItemAsync(LocalStorageKeys.VanityUrl);
    }
}