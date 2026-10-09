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
using Microsoft.Extensions.Options;
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
        var steamSection = configuration.GetSection("Steam");
        services.AddSingleton<IValidateOptions<SteamClientApiOptions>>(new SteamClientApiOptionsValidator(steamSection));
        services.AddOptions<SteamClientApiOptions>()
            .Bind(steamSection)
            .ValidateOnStart();

        // Cache Provider: consume the same validated options as the HTTP clients.
        services.AddJoyfulReaperSqliteDistributedCache(options =>
        {
            options.BasePath = Path.Combine(AppContext.BaseDirectory, "Data");
        });
        services.AddOptions<SqliteDistributedCacheOptions>()
            .Configure<IOptions<SteamClientApiOptions>>((options, steam) =>
                options.ConnectionString = steam.Value.ConnectionString);

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
