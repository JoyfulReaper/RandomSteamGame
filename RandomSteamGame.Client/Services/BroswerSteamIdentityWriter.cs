/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.JSInterop;
using RandomSteamGame.Shared.Interfaces;

namespace RandomSteamGame.Client.Services;

public class BrowserSteamIdentityWriter : ISteamIdentityWriter
{
    private readonly IJSRuntime _js;

    public BrowserSteamIdentityWriter(IJSRuntime js)
    {
        _js = js;
    }

    public async Task SetIdentityAsync(string steamId, string? vanityUrl)
    {
        await _js.InvokeVoidAsync("eval", $"document.cookie = \"SteamId={steamId}; path=/; max-age=31536000\"");
        await _js.InvokeVoidAsync("eval", $"document.cookie = \"VanityUrl={vanityUrl ?? ""}; path=/; max-age=31536000\"");
    }

    public async Task ClearAsync()
    {
        await _js.InvokeVoidAsync("eval", "document.cookie = \"SteamId=; path=/; max-age=0\"");
        await _js.InvokeVoidAsync("eval", "document.cookie = \"VanityUrl=; path=/; max-age=0\"");
    }
}