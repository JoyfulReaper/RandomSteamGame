/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using RandomSteamGame.Client.Interop;
using RandomSteamGame.Shared.Interfaces;
using System.Runtime.Versioning;

namespace RandomSteamGame.Client.Services;

[SupportedOSPlatform("browser")]
public sealed class BrowserSteamIdentityReader : ISteamIdentityReader
{
    public ValueTask<string?> GetSteamIdAsync()
        => ValueTask.FromResult<string?>(CookieInterop.GetCookie("SteamId"));

    public ValueTask<string?> GetVanityUrlAsync()
        => ValueTask.FromResult<string?>(CookieInterop.GetCookie("VanityUrl"));
}