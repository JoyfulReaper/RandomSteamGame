/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace RandomSteamGame.Shared.Services;

public static class SteamPlaytimeHelper
{
    public static int GetDisplayPlaytimeMinutes(
        int playtimeForever,
        int playtimeWindowsForever,
        int playtimeMacForever,
        int playtimeLinuxForever,
        int playtime2Weeks)
    {
        var platformTotal = playtimeWindowsForever + playtimeMacForever + playtimeLinuxForever;

        return Math.Max(
            playtimeForever,
            Math.Max(platformTotal, playtime2Weeks));
    }
}
