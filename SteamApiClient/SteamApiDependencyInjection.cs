/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SteamApiClient.HttpClients;
using SteamApiClient.Services;
using SteamApiClient.Settings;


namespace SteamApiClient;

public static class SteamApiDependencyInjection
{
    public static IServiceCollection AddSteamApiClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind settings
        services.Configure<SteamClientApiOptions>(configuration.GetSection("SteamOptions"));

        // Register core clients and wrappers
        services.AddHttpClient<ISteamStoreClient, SteamStoreClient>();
        services.AddHttpClient<ISteamClient, SteamClient>();

        services.AddScoped<ICacheService, CacheService>();

        // Enable Hybrid Caching
        services.AddHybridCache();

        return services;
    }
}