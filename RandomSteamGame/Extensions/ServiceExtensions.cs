/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.JRData;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using RandomSteamGame.Client.Services;
using RandomSteamGame.Common.Errors;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Interfaces;
using RandomSteamGame.Shared.Services;
using SteamApiClient;
using SteamApiClient.Caching;
using SteamApiClient.Settings;
using System.Threading.RateLimiting;

namespace RandomSteamGame.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        SqliteProviderInitializer.Initialize();

        const string schemaSql = """
            CREATE TABLE IF NOT EXISTS AppStats (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                RandomGamesGenerated INTEGER NOT NULL DEFAULT 0
            );

            INSERT INTO AppStats (Id, RandomGamesGenerated)
            SELECT 1, 0
            WHERE NOT EXISTS (SELECT 1 FROM AppStats WHERE Id = 1);
            """;

        var connectionString = SqliteHelper.InitializeSqlite("kgivler_com.db", schemaSql);
        var steamOptions = GetSteamOptions(config);

        services.AddBlazorServices();
        services.AddApiServices();
        services.AddSteamIdentityServices();
        services.AddGameProviderServices();
        services.AddPersistenceServices(connectionString);
        services.AddSteamServices(config);
        services.AddApplicationCors(config, env);
        services.AddSteamRateLimiting(steamOptions.RateLimiting);
        services.AddMemoryCache();
        services.AddHttpClient<RandomSteamApiClient>();
        services.AddScoped<IBetaAvailabilityService, BetaAvailabilityService>();

        ValidateSteamApiKey(steamOptions);

        return services;
    }

    private static IServiceCollection AddBlazorServices(this IServiceCollection services)
    {
        services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        return services;
    }

    private static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddHttpContextAccessor();
        services.AddProblemDetails();
        services.AddTransient<ProblemDetailsFactory, RandomSteamProblemDetailsFactory>();

        return services;
    }

    private static IServiceCollection AddSteamIdentityServices(this IServiceCollection services)
    {
        services.AddScoped<IBrowserSteamIdentityStore, BrowserSteamIdentityStore>();
        services.AddScoped<ISteamIdentityWriter, ServerSteamIdentityWriter>();
        services.AddScoped<ISteamIdentityReader, ServerSteamIdentityReader>();

        return services;
    }

    private static IServiceCollection AddGameProviderServices(this IServiceCollection services)
    {
        var providerType = typeof(IGameProvider);
        var implementations = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => providerType.IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);

        foreach (var implementation in implementations)
        {
            services.AddScoped(providerType, implementation);
        }

        services.AddScoped<GameProviderFactory>();
        services.AddScoped<IOwnedGamesCacheResetTracker, OwnedGamesCacheResetTracker>();

        return services;
    }

    private static IServiceCollection AddPersistenceServices(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<IHtmlSanitizerService, HtmlSanitizerService>();
        services.AddScoped<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IAppStatsService, AppStatsService>();
        services.AddScoped<SqliteConnection>(_ => new SqliteConnection(connectionString));

        return services;
    }

    private static IServiceCollection AddSteamServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddSteamApiClient(config);
        return services;
    }

    private static IServiceCollection AddApplicationCors(
        this IServiceCollection services,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        services.AddCors(options =>
        {
            var configuredOrigins = config
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? [];

            var allowedOrigins = new List<string>(configuredOrigins);

            if (env.IsDevelopment())
            {
                allowedOrigins.Add("http://localhost:5500");
                allowedOrigins.Add("http://127.0.0.1:5500");
                allowedOrigins.Add("http://localhost:3000");
            }

            if (allowedOrigins.Count == 0)
            {
                throw new InvalidOperationException("No CORS origins configured.");
            }

            options.AddPolicy("DefaultCors", policy =>
            {
                policy.WithOrigins(allowedOrigins.ToArray())
                    .WithMethods("GET", "POST")
                    .WithHeaders("Content-Type", "Authorization");
            });
        });

        return services;
    }

    private static IServiceCollection AddSteamRateLimiting(
        this IServiceCollection services,
        RateLimitingOptions rateLimiting)
    {
        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("steam_api_limiter", limiterOptions =>
            {
                limiterOptions.Window = TimeSpan.FromSeconds(rateLimiting.WindowSeconds);
                limiterOptions.PermitLimit = rateLimiting.PermitLimit;
                limiterOptions.QueueLimit = 0;
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsync("Too many requests. Please slow down and try again in a few seconds.", token);
            };
        });

        return services;
    }

    private static SteamClientApiOptions GetSteamOptions(IConfiguration config)
    {
        return config.GetSection("Steam").Get<SteamClientApiOptions>()
            ?? throw new InvalidOperationException("Steam configuration is missing.");
    }

    private static void ValidateSteamApiKey(SteamClientApiOptions steamOptions)
    {
        if (string.IsNullOrWhiteSpace(steamOptions.ApiKey) ||
            steamOptions.ApiKey == "STEAM_API_KEY" ||
            steamOptions.ApiKey.Length < 32)
        {
            throw new InvalidOperationException("CRITICAL: Invalid Steam API Key.");
        }
    }
}
