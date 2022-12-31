using Microsoft.Extensions.DependencyInjection;
using SteamApiClient.Options;
using SteamApiClient.HttpClients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;

namespace SteamApiClient;

public static class DependencyInjection
{
    public static IServiceCollection AddSteamApiClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SteamOptions>(
            configuration.GetSection(nameof(SteamOptions)));

        services.Configure<DistributedCacheEntryOptions>(
            configuration.GetSection(nameof(DistributedCacheEntryOptions)));

        services.AddHttpClient<SteamClient>();
        services.AddHttpClient<SteamStoreClient>();

        services.AddDistributedSqlServerCache(opts =>
        {
            opts.ConnectionString = configuration.GetConnectionString("DefaultConnection");
            opts.SchemaName = "dbo";
            opts.TableName = "DataCache";
        });

        return services;
    }
}
