/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.AspNetCore.Components;
using RandomSteamGame.Shared.Contracts;
using System.Net.Http.Json;
using System.Text.Json;

namespace RandomSteamGame.Client.Services;

public sealed class RandomSteamApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RandomSteamApiClient> _logger;

    public RandomSteamApiClient(
        HttpClient httpClient,
        NavigationManager navigationManager,
        ILogger<RandomSteamApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri(navigationManager.BaseUri);
        }
    }

    public Task<ApiResult<OwnedGamesResponse>> GetOwnedGamesAsync(
        string provider,
        long steamId,
        CancellationToken cancellationToken = default) =>
        GetFromJsonAsync<OwnedGamesResponse>(
            $"api/{Uri.EscapeDataString(provider)}/{steamId}/library",
            cancellationToken);

    public Task<ApiResult<bool>> InvalidateOwnedGamesCacheAsync(
        string provider,
        long steamId,
        CancellationToken cancellationToken = default) =>
        SendForNoContentAsync(
            requestUri: $"api/{Uri.EscapeDataString(provider)}/{steamId}/library/refresh",
            sendAsync: () => _httpClient.PostAsync(
                $"api/{Uri.EscapeDataString(provider)}/{steamId}/library/refresh",
                content: null,
                cancellationToken),
            cancellationToken);

    public Task<ApiResult<GameDetails>> GetRandomGameDetailsAsync(
        string provider,
        long? steamId = null,
        string? vanityUrl = null,
        bool unplayedOnly = false,
        CancellationToken cancellationToken = default) =>
        GetFromJsonAsync<GameDetails>(
            BuildIdentifierUri(
                provider,
                "random-game/details",
                steamId,
                vanityUrl,
                unplayedOnly),
            cancellationToken);

    public Task<ApiResult<long>> ResolveVanityUrlAsync(
        string provider,
        string vanityUrl,
        CancellationToken cancellationToken = default) =>
        GetFromJsonAsync<long>(
            $"api/{Uri.EscapeDataString(provider)}/resolve/{Uri.EscapeDataString(vanityUrl)}",
            cancellationToken);

    public Task<ApiResult<AppStatsResponse>> RecordHitAsync(CancellationToken cancellationToken = default) =>
        SendForJsonAsync<AppStatsResponse>(
            requestUri: "api/stats/hit",
            sendAsync: () => _httpClient.PostAsync("api/stats/hit", content: null, cancellationToken),
            cancellationToken);

    public Task<ApiResult<AppStatsResponse>> GetStatsAsync(CancellationToken cancellationToken = default) =>
        GetFromJsonAsync<AppStatsResponse>("api/stats", cancellationToken);

    private Task<ApiResult<T>> GetFromJsonAsync<T>(
        string requestUri,
        CancellationToken cancellationToken) =>
        SendForJsonAsync<T>(
            requestUri,
            () => _httpClient.GetAsync(requestUri, cancellationToken),
            cancellationToken);

    private async Task<ApiResult<T>> SendForJsonAsync<T>(
        string requestUri,
        Func<Task<HttpResponseMessage>> sendAsync,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await sendAsync();

            if (!response.IsSuccessStatusCode)
            {
                var problem = await TryReadProblemAsync(response, cancellationToken);

                _logger.LogDebug(
                    "API returned status code {StatusCode} for {RequestUri}",
                    response.StatusCode,
                    requestUri);

                return ApiResult<T>.Failure(
                    response.StatusCode,
                    problem,
                    $"Request to '{requestUri}' failed.");
            }

            var payload = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            if (payload is null)
            {
                return ApiResult<T>.Failure(
                    response.StatusCode,
                    errorMessage: $"API returned empty data for '{requestUri}'.");
            }

            return ApiResult<T>.Success(payload);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed API request to {RequestUri}", requestUri);
            return ApiResult<T>.Failure(
                statusCode: null,
                errorMessage: "Unable to reach the server right now.");
        }
    }

    private async Task<ApiResult<bool>> SendForNoContentAsync(
        string requestUri,
        Func<Task<HttpResponseMessage>> sendAsync,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await sendAsync();

            if (!response.IsSuccessStatusCode)
            {
                var problem = await TryReadProblemAsync(response, cancellationToken);

                _logger.LogDebug(
                    "API returned status code {StatusCode} for {RequestUri}",
                    response.StatusCode,
                    requestUri);

                return ApiResult<bool>.Failure(
                    response.StatusCode,
                    problem,
                    $"Request to '{requestUri}' failed.");
            }

            return ApiResult<bool>.Success(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed API request to {RequestUri}", requestUri);
            return ApiResult<bool>.Failure(
                statusCode: null,
                errorMessage: "Unable to reach the server right now.");
        }
    }

    private static async Task<ApiProblem?> TryReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ApiProblem>(cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private static string BuildIdentifierUri(
        string provider,
        string route,
        long? steamId,
        string? vanityUrl,
        bool unplayedOnly)
    {
        var queryParts = new List<string>();

        if (steamId.HasValue)
        {
            queryParts.Add($"userId={steamId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(vanityUrl))
        {
            queryParts.Add($"vanityUrl={Uri.EscapeDataString(vanityUrl)}");
        }

        if (unplayedOnly)
        {
            queryParts.Add("unplayedOnly=true");
        }

        var queryString = queryParts.Count == 0
            ? string.Empty
            : $"?{string.Join("&", queryParts)}";

        return $"api/{Uri.EscapeDataString(provider)}/{route}{queryString}";
    }
}
