using SteamApiClient.HttpClients;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace SteamApiClient;

public static class DependencyInjection
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

        services.AddHttpClient<SteamClient>();
        services.AddHttpClient<SteamStoreClient>();

        services.AddDistributedSqlServerCache(opts =>
        {
            opts.ConnectionString = configuration.GetConnectionString(steamOptions.ConnectionString);
            opts.SchemaName = steamOptions.CacheSchema;
            opts.TableName = steamOptions.CacheTable;
        });

        return services;
    }
}
