﻿using System.Net.Http.Headers;
using System.Net.Http;
using SteamApiClient.Contracts.SteamApi;
using System.Net.Http.Json;
using SteamApiClient.Exceptions;
using SteamApiClient.Options;
using Microsoft.Extensions.Options;

namespace SteamApiClient.HttpClients;

public class SteamClient
{
    private readonly HttpClient _httpClient;
    private readonly SteamOptions _steamOptions;

    public SteamClient(HttpClient httpClient, IOptions<SteamOptions> steamOptions)
    {
        _httpClient = httpClient;
        _steamOptions = steamOptions.Value;

        _httpClient.BaseAddress = new Uri("https://api.steampowered.com");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(SteamClientConstants.UserAgent, SteamClientConstants.Version));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(SteamClientConstants.UserAgentComment));
    }

    public async Task<OwnedGames> GetOwnedGames(Int64 steamId)
    {
        var output =
            await _httpClient.GetFromJsonAsync<OwnedGamesResponse>(
                $"/IPlayerService/GetOwnedGames/v0001/?key={_steamOptions.ApiKey}&steamid={steamId}&format=json");

        return output!.Response;
    }

    public async Task<Int64> GetSteamIdFromVanityUrl(string vanityUrl)
    {
        var output =
            await _httpClient.GetFromJsonAsync<ResolveVanityUrlResponse>(
                $"/ISteamUser/ResolveVanityURL/v0001/?key={_steamOptions.ApiKey}&vanityurl={vanityUrl}&format=json");

        if (output.Response.Success != 1)
        {
            throw new VanityResolutionException("Unable to resolve vanity url");
        }

        return Int64.Parse(output.Response.SteamId!);
    }
}
