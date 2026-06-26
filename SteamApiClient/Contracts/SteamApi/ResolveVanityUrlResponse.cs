/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace SteamApiClient.Contracts.SteamApi;

public record ResolveVanityUrlResponse(
    ResolvedVanityUrl Response
);

public record ResolvedVanityUrl(
    string SteamId,
    int Success
);