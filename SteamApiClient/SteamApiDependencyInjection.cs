/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeoSmart.Caching.Sqlite;
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

        // Cache Provider Logic
        if (steamOptions.CacheProvider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
        {
            // Extract filename from "Data Source=filename.db"
            var fileName = steamOptions.ConnectionString.Replace("Data Source=", "", StringComparison.OrdinalIgnoreCase).Trim();
            var dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            Directory.CreateDirectory(dataFolder);

            services.AddSqliteCache(options =>
            {
                options.CachePath = Path.Combine(dataFolder, fileName);
            });
        }
        else if (steamOptions.CacheProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDistributedSqlServerCache(options =>
            {
                options.ConnectionString = steamOptions.ConnectionString;
                options.SchemaName = steamOptions.CacheSchema;
                options.TableName = steamOptions.CacheTable;
            });
        }
        else
        {
            throw new InvalidOperationException($"Unsupported CacheProvider: {steamOptions.CacheProvider}. Use 'SQLite' or 'SqlServer'.");
        }

        // ADD HYBRID CACHE (L1 In-Memory + L2 Distributed)
        services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                // The total time the item lives in the L2 Distributed Cache (SQLite)
                Expiration = TimeSpan.FromHours(24),

                // The time the item lives in the L1 In-Memory Cache before checking L2
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
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            });

        services.AddHttpClient<ISteamClient, SteamClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.steampowered.com/");
        })
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            });

        services.AddScoped<ICacheService, CacheService>();
        services.AddHybridCache();

        return services;
    }
}