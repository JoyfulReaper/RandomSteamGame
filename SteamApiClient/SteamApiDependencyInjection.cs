using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NeoSmart.Caching.Sqlite;
using SteamApiClient.HttpClients;
using SteamApiClient.Settings;

namespace SteamApiClient;

public static class SteamApiDependencyInjection
{
    public static IServiceCollection AddSteamApiClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var steamOptions = services.Configure<SteamOptions>(
            configuration.GetSection("SteamOptions"));

        services.Configure<CacheSettings>(
            configuration.GetSection("Cache"));

        services.AddHttpClient<ISteamClient, SteamClient>();
        services.AddHttpClient<ISteamStoreClient, SteamStoreClient>();

        var cacheProvider = configuration.GetValue<string>("CacheProvider") ?? "SqlServer";

        // Use SQLite
        // Set CacheProvider to "Sqlite" in appsettings.json
        if (cacheProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var dataFolder = Path.Combine(baseDirectory, "Data");
            Directory.CreateDirectory(dataFolder);

            var cacheFile = configuration.GetConnectionString("SqliteCacheConnection");
            var cachePath = Path.Combine(dataFolder, cacheFile);

            services.AddSqliteCache(options =>
            {
                options.CachePath = cachePath;
            });
        }
        else // Default to SQL Server
        {
            services.AddDistributedSqlServerCache(opts =>
            {
                opts.ConnectionString =
                    configuration.GetConnectionString("SqlServerConnection");

                opts.SchemaName = "dbo";
                opts.TableName = "Cache";
            });
        }

        return services;
    }
}