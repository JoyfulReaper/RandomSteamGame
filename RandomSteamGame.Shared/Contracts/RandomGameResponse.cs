/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */


namespace RandomSteamGame.Shared.Contracts;

public record RandomGameResponse(
    long SteamId,
    int AppId,
    SteamAppInformation AppInformation,
    int PlaytimeForever,
    long RTimeLastPlayed,
    int Playtime2Weeks);

public record SteamAppInformation(
    string Name,
    int SteamAppId,
    bool IsFree,
    string AboutTheGame,
    string DetailedDescription,
    string ShortDescription,
    string Background,
    string BackgroundRaw);