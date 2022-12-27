using RandomSteamGame.Options;
using RandomSteamGame.Services;

namespace RandomSteamGame;

public static class DependencyInjection
{
    public static IServiceCollection AddRandomSteamGame(this IServiceCollection services, IConfiguration configuration)
    {
        AddOptions(services, configuration);
        AddHttpClients(services);

        services.AddScoped<SteamService>();

        return services;
    }

    public static IServiceCollection AddHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient<SteamClient>();
        services.AddHttpClient<SteamStoreClient>();

        return services;
    }

    public static IServiceCollection AddOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SteamOptions>(
            configuration.GetSection(nameof(SteamOptions)));

        services.Configure<MonkeyCacheOptions>(
            configuration.GetSection(nameof(MonkeyCacheOptions)));

        return services;
    }
}
