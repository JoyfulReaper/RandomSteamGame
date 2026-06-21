using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NeoSmart.Caching.Sqlite;
using SteamApiClient.HttpClients;

namespace SteamApiClient;

public static class SteamApiDependencyInjection
{
    public static IServiceCollection AddSteamApiClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var steamOptions = new SteamOptions();
        configuration.Bind(nameof(steamOptions), steamOptions);
        services.AddSingleton(Options.Create(steamOptions));

        services.Configure<DistributedCacheEntryOptions>(
            configuration.GetSection(nameof(DistributedCacheEntryOptions)));

        services.AddHttpClient<ISteamClient, SteamClient>();
        services.AddHttpClient<ISteamStoreClient, SteamStoreClient>();

        var cacheProvider = configuration.GetValue<string>("CacheProvider") ?? "SqlServer";

        // Use SQLite
        if (cacheProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSqliteCache(options =>
            {
                options.CachePath = configuration.GetConnectionString("SqliteCacheConnection") ?? "steam_cache.db";
            });
        }
        else // Default to SQL Server
        {
            services.AddDistributedSqlServerCache(opts =>
            {
                opts.ConnectionString = configuration.GetConnectionString(steamOptions.ConnectionString);
                opts.SchemaName = steamOptions.CacheSchema;
                opts.TableName = steamOptions.CacheTable;
            });
        }

        return services;
    }
}