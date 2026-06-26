/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */


using SteamApiClient.Contracts.SteamStoreApi;

namespace SteamApiClient.HttpClients;

public interface ISteamStoreClient
{
    Task<AppDetailsResponse> GetAppData(
        int appId,
        CancellationToken ct = default);
}