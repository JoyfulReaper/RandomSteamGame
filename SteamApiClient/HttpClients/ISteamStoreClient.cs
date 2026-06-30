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
    Task<AppData?> GetAppData(
        int appId,
        IEnumerable<string>? tags = null,
        CancellationToken ct = default);
}