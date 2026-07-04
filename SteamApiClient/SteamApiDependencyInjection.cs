/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.Caching.Sqlite;
using Microsoft.Extensions.Caching.Hybrid;
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
        // Cache Provider
        var steamOptions = configuration.GetSection("Steam").Get<SteamClientApiOptions>()
                           ?? throw new InvalidOperationException("Steam configuration is missing.");

        services.AddJoyfulReaperSqliteDistributedCache(options =>
        {
            options.ConnectionString = steamOptions.ConnectionString;
            options.BasePath = Path.Combine(AppContext.BaseDirectory, "Data");
        });

        // Add hybrid cache (L1 In-Memory + L2 Distributed)
        services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                // The total time the item lives in the L2 Distributed Cache (SQLite): TODO make configurable through appsettings
                Expiration = TimeSpan.FromHours(24),

                // The time the item lives in the L1 In-Memory Cache before checking L2: TODO make configurable through appsettings
                LocalCacheExpiration = TimeSpan.FromMinutes(30)
            };
        });

        services.AddOptions<SteamClientApiOptions>()
            .Bind(configuration.GetSection("Steam"))
            .ValidateDataAnnotations()
            .Validate(options => !string.IsNullOrEmpty(options.ApiKey), "Steam API Key is required")
            .ValidateOnStart();

        services.AddHttpClient<ISteamStoreClient, SteamStoreClient>(client =>
        {
            client.BaseAddress = new Uri("https://store.steampowered.com/");
        })
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 3; // TODO: Make configurable through appsettings
                options.Retry.Delay = TimeSpan.FromSeconds(2); // TODO: Make configurable through appsettings
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            });

        services.AddHttpClient<ISteamClient, SteamClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.steampowered.com/");
        })
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 3; // TODO: Make configurable through appsettings
                options.Retry.Delay = TimeSpan.FromSeconds(2); // TODO: Make configurable through appsettings
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            });

        services.AddScoped<ICacheService, CacheService>();

        return services;
    }
}
