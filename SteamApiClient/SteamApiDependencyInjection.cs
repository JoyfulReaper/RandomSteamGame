/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.Configure<SteamClientApiOptions>(configuration.GetSection("SteamOptions"));

        services.AddHttpClient<ISteamStoreClient, SteamStoreClient>()
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            });

        services.AddHttpClient<ISteamClient, SteamClient>()
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