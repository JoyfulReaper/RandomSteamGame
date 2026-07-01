/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SteamApiClient.Caching;
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
        SqliteProviderInitializer.Initialize();

        // Cache Provider
        var steamOptions = configuration.GetSection("Steam").Get<SteamClientApiOptions>()
                           ?? throw new InvalidOperationException("Steam configuration is missing.");

        // Cache Provider Logic
        if (steamOptions.CacheProvider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
        {
            var dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            Directory.CreateDirectory(dataFolder);
                    options.ConnectionString = BuildSqliteConnectionString(steamOptions.ConnectionString);
                });

            services.AddSingleton<Microsoft.Extensions.Caching.Distributed.IDistributedCache, SqliteDistributedCache>();
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

    private static string BuildSqliteConnectionString(string configuredConnectionString)
    {
        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(configuredConnectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource))
        {
            throw new InvalidOperationException("SQLite cache connection string must include a Data Source.");
        }

        if (!Path.IsPathRooted(builder.DataSource))
        {
            var dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            Directory.CreateDirectory(dataFolder);
            builder.DataSource = Path.Combine(dataFolder, builder.DataSource);
        }

        return builder.ToString();
    }
}
