/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.JRData;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Mythetech.LocalStorage;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Interfaces;
using SteamApiClient;
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
        // ==========================================
        // SERVICES CONFIGURATION (DI CONTAINER)
        // ==========================================
        var connectionString = SqliteHelper.InitializeSqlite("kgivler_com.db", null);

        // Blazor Interactive Auto Render Mode Engines
        services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        services.AddControllers();
        services.AddHttpContextAccessor();
        services.AddProblemDetails();

        services.AddSteamApiClient(config);
        services.AddScoped<ISteamIdentityService, ManualSteamIdentityService>();

        // Game Providers
        var providerType = typeof(IGameProvider);
        var implementations = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => providerType.IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);

        foreach (var type in implementations)
        {
            services.AddScoped(providerType, type);
        }

        services.AddScoped<GameProviderFactory>();
        services.AddScoped<IHtmlSanitizerService, HtmlSanitizerService>();
        services.AddScoped<SqliteConnection>(_ =>
                    new SqliteConnection(connectionString));

        services.AddLocalStorage();

        var steamSection = config.GetSection("Steam");
        services.Configure<SteamClientApiOptions>(steamSection);
        var steamOptions = steamSection.Get<SteamClientApiOptions>()
                           ?? throw new InvalidOperationException("Steam configuration is missing.");

        // CORS Configuration
        services.AddCors(options =>
        {
            var configuredOrigins = config
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? [];

            var allowedOrigins = new List<string>(configuredOrigins);

            // Dynamically inject local development tools if running locally
            if (env.IsDevelopment())
            {
                allowedOrigins.Add("http://localhost:5500");   // VS Code Live Server
                allowedOrigins.Add("http://127.0.0.1:5500");   // Local loopback address
                allowedOrigins.Add("http://localhost:3000");   // Typical SPA dev port
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

        // Rate Limting
        var rlOptions = steamOptions.RateLimiting;

        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("steam_api_limiter", limiterOptions =>
            {
                limiterOptions.Window = TimeSpan.FromSeconds(rlOptions.WindowSeconds); // 10 second window
                limiterOptions.PermitLimit = rlOptions.PermitLimit;                  // Max 20 requests per window
                limiterOptions.QueueLimit = 0;                   // Reject requests immediately if over limit
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });

            // Custom response when rate limited
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsync("Too many requests. Please slow down and try again in a few seconds.", token);
            };
        });

        // Api key validation
        if (string.IsNullOrWhiteSpace(steamOptions.ApiKey) ||
            steamOptions.ApiKey == "STEAM_API_KEY" ||
            steamOptions.ApiKey.Length < 32)
        {

            throw new InvalidOperationException("CRITICAL: Invalid Steam API Key.");
        }

        services.AddHttpClient("ApiClient", (sp, client) =>
        {
            var navManager = sp.GetRequiredService<NavigationManager>();
            client.BaseAddress = new Uri(navManager.BaseUri);
        });

        return services;
    }
}