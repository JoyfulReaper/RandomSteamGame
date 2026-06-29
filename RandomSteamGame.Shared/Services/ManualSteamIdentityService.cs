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

public class BrowserSteamIdentityService : ISteamIdentityReader
{
    private readonly ILocalStorageService _localStorage;

    public BrowserSteamIdentityService(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    public ValueTask<long?> GetSteamIdAsync()
        => _localStorage.GetItemAsync<long?>(LocalStorageKeys.SteamId);

    public ValueTask<string?> GetVanityUrlAsync()
        => _localStorage.GetItemAsync<string>(LocalStorageKeys.VanityUrl);
}