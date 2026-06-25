/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

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
        // Configuration
        var steamSection = configuration.GetSection("SteamClientApiOptions");
        services.Configure<SteamClientApiOptions>(steamSection);

        var options = new SteamClientApiOptions();
        steamSection.Bind(options);

        // Services
        services.AddHttpClient<ISteamStoreClient, SteamStoreClient>();
        services.AddHttpClient<ISteamClient, SteamClient>();
        services.AddScoped<ICacheService, CacheService>();

        services.AddHybridCache();

        // Cache Provider
        var cacheProvider = configuration.GetValue<string>("CacheProvider");
        if (string.IsNullOrWhiteSpace(cacheProvider))
        {
            throw new InvalidOperationException(
                "The configuration key 'CacheProvider' is missing or empty. " +
                "You must explicitly set it to either 'Sqlite' or 'SqlServer'.");
        }

        if (cacheProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var dataFolder = Path.Combine(baseDirectory, "Data");
            Directory.CreateDirectory(dataFolder);

            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                throw new InvalidOperationException("Sqlite caching is enabled but the ConnectionString is missing from configuration.");
            }

            var cachePath = Path.Combine(dataFolder, options.ConnectionString);

            services.AddSqliteCache(opts =>
            {
                opts.CachePath = cachePath;
            });
        }
        else if (cacheProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                throw new InvalidOperationException("SQL Server caching is enabled but the ConnectionString is missing from configuration.");
            }

            services.AddDistributedSqlServerCache(opts =>
            {
                opts.ConnectionString = options.ConnectionString;
                opts.SchemaName = string.IsNullOrWhiteSpace(options.CacheSchema) ? "dbo" : options.CacheSchema;
                opts.TableName = string.IsNullOrWhiteSpace(options.CacheTable) ? "Cache" : options.CacheTable;
            });
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported CacheProvider configuration value: '{cacheProvider}'. " +
                "Valid options are 'Sqlite' or 'SqlServer'.");
        }

        return services;
    }
}