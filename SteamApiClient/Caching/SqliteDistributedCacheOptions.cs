/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace SteamApiClient.Caching;

public sealed class SqliteDistributedCacheOptions
{
    public required string ConnectionString { get; set; }
}
