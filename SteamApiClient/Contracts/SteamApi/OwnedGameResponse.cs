/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using System.Text.Json.Serialization;

namespace SteamApiClient.Contracts.SteamApi;

public record OwnedGamesResponse(
    [property: JsonPropertyName("response")] OwnedGames Response
);

public record OwnedGames(
    [property: JsonPropertyName("game_count")] int GameCount,
    [property: JsonPropertyName("games")] List<Game> Games
);

public record Game(
    [property: JsonPropertyName("appid")] int AppId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("playtime_forever")] int PlaytimeForever,
    [property: JsonPropertyName("img_icon_url")] string? ImgIconUrl,
    [property: JsonPropertyName("playtime_windows_forever")] int PlaytimeWindowsForever,
    [property: JsonPropertyName("playtime_mac_forever")] int PlaytimeMacForever,
    [property: JsonPropertyName("playtime_linux_forever")] int PlaytimeLinuxForever,
    [property: JsonPropertyName("rtime_last_played")] long RTimeLastPlayed,
    [property: JsonPropertyName("playtime_2weeks")] int Playtime2Weeks
);