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

using System.ComponentModel.DataAnnotations;

namespace SteamApiClient.Settings;

public record SteamClientApiOptions
{
    [Required(ErrorMessage = "Steam API Key is missing!")]
    public string ApiKey { get; init; } = default!;
    [Required]
    public string ConnectionString { get; init; } = default!;
    [Required]
    public string CacheSchema { get; init; } = default!;
    [Required]
    public string CacheTable { get; init; } = default!;

    [Required]
    public string CacheProvider { get; init; } = default!;

    [Required]
    public CacheSettings Cache { get; init; } = new();

    [Required]
    public RateLimitingOptions RateLimiting { get; init; } = new();
}

public record CacheSettings
{
    [Required]
    public CachePolicy OwnedGames { get; init; } = default!;
    [Required]
    public CachePolicy AppDetails { get; init; } = default!;
    [Required]
    public CachePolicy VanitySuccess { get; init; } = default!;
    [Required]
    public CachePolicy VanityNotFound { get; init; } = default!;
}

public record CachePolicy
{
    public int AbsoluteMinutes { get; init; }

    public TimeSpan Duration =>
        TimeSpan.FromMinutes(AbsoluteMinutes);
}

public record RateLimitingOptions
{
    public int PermitLimit { get; init; } = 20; // 20 requests per window
    public int WindowSeconds { get; init; } = 10; // Ten second window
}