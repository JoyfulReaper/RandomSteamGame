/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using RandomSteamGame.Client.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using System.Net.Http.Json;

namespace RandomSteamGame.Client.Services;

public class GameApiClient : IGameApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<GameApiClient> _logger;

    public GameApiClient(HttpClient http, ILogger<GameApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<OwnedGamesResponse?> GetOwnedGamesAsync(long steamId) =>
        await GetFromJsonSafeAsync<OwnedGamesResponse>($"api/steam/{steamId}/library");

    public async Task<RandomGameResponse?> GetRandomSteamGameAsync(long steamId) =>
        await GetFromJsonSafeAsync<RandomGameResponse>($"api/steam/random-game?userId={steamId}");

    public async Task<GameDetails?> GetRandomGameBySteamIdAsync(long steamId) =>
        await GetFromJsonSafeAsync<GameDetails>($"api/steam/random-game/details?userId={steamId}");

    public async Task<GameDetails?> GetRandomGameByVanityUrlAsync(string vanityUrl) =>
        await GetFromJsonSafeAsync<GameDetails>($"api/steam/random-game/details?vanityUrl={Uri.EscapeDataString(vanityUrl)}");

    public async Task<long?> ResolveVanityUrlAsync(string vanityUrl) =>
        await GetFromJsonSafeAsync<long>($"api/steam/resolve/{Uri.EscapeDataString(vanityUrl)}");

    private async Task<T?> GetFromJsonSafeAsync<T>(string url)
    {
        try
        {
            var response = await _http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<T>();
            }

            // Log backend RFC 7807 problem details or custom ErrorOr payloads here
            _logger.LogDebug("API returned non-success status code: {StatusCode} for URL: {Url}", response.StatusCode, url);
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch data from {Url}", url);
            return default;
        }
    }
}
