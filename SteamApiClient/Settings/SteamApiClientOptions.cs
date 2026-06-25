/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

//{
//    "Steam": {
//        "ApiKey": "YOUR_STEAM_KEY",
//    "ConnectionString": "Data Source=steam_cache.db",
//    "CacheSchema": "dbo",
//    "CacheTable": "SteamCache",
//    "Cache": {
//      "OwnedGames": { "AbsoluteMinutes": 60 },
//      "AppDetails": { "AbsoluteMinutes": 1440 },
//      "VanitySuccess": { "AbsoluteMinutes": 10080 },
//      "VanityNotFound": { "AbsoluteMinutes": 1440 }
//        }
//    }
//}

namespace SteamApiClient.Settings;

public record SteamClientApiOptions
{
    public string ApiKey { get; init; } = default!;
    public string ConnectionString { get; init; } = default!;
    public string CacheSchema { get; init; } = default!;
    public string CacheTable { get; init; } = default!;

    public CacheSettings Cache { get; init; } = new();
}

public record CacheSettings
{
    public CachePolicy OwnedGames { get; init; } = default!;
    public CachePolicy AppDetails { get; init; } = default!;
    public CachePolicy VanitySuccess { get; init; } = default!;
    public CachePolicy VanityNotFound { get; init; } = default!;
}

public record CachePolicy
{
    public int AbsoluteMinutes { get; init; }

    public TimeSpan Duration => TimeSpan.FromMinutes(AbsoluteMinutes);
}