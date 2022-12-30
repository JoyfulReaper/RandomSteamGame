using Microsoft.Extensions.DependencyInjection;
using SteamApiClient.Options;
using SteamApiClient.HttpClients;
using Microsoft.Extensions.Configuration;

namespace SteamApiClient;

public static class DependencyInjection
{
    public static IServiceCollection AddSteamApiClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SteamOptions>(
            configuration.GetSection(nameof(SteamOptions)));

        services.AddHttpClient<SteamClient>();
        services.AddHttpClient<SteamStoreClient>();

        return services;
    }
}
