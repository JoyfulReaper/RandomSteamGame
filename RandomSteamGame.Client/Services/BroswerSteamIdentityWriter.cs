/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using RandomSteamGame.Client.Interop;
using RandomSteamGame.Shared.Contracts;
using RandomSteamGame.Shared.Interfaces;
using System.Runtime.Versioning;

namespace RandomSteamGame.Client.Services;

[SupportedOSPlatform("browser")]
public sealed class BrowserSteamIdentityWriter : ISteamIdentityWriter
{
    public Task SetIdentityAsync(SteamIdentity steamIdentity)
    {
        if (string.IsNullOrWhiteSpace(steamIdentity.SteamId))
        {
            return Task.CompletedTask;
        }

        CookieInterop.SetCookie("SteamId", steamIdentity.SteamId, 365);
        CookieInterop.DeleteCookie("VanityUrl");

        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        CookieInterop.DeleteCookie("SteamId");
        CookieInterop.DeleteCookie("VanityUrl");

        return Task.CompletedTask;
    }
}
