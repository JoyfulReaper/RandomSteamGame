/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.JRData;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Mythetech.LocalStorage;
using NeoSmart.Caching.Sqlite;
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

        services.AddSteamApiClient(config); // Steam Api Client
        services.AddScoped<ISteamIdentityService, ServerSteamIdentityService>();
        services.AddScoped<ISteamService, SteamService>();
        services.AddScoped<IHtmlSanitizerService, HtmlSanitizerService>();
        services.AddScoped<SqliteConnection>(_ =>
                    new SqliteConnection(connectionString));

        services.AddLocalStorage(); // TODO: Think about possibly rolling our own or finding a different solution

        // Cache Provider
        var steamOptions = config.GetSection("Steam").Get<SteamClientApiOptions>()
                           ?? throw new InvalidOperationException("Steam configuration is missing.");

        // Cache Provider Logic
        if (steamOptions.CacheProvider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
        {
            // Extract filename from "Data Source=filename.db"
            var fileName = steamOptions.ConnectionString.Replace("Data Source=", "", StringComparison.OrdinalIgnoreCase).Trim();
            var dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            Directory.CreateDirectory(dataFolder);

            services.AddSqliteCache(options =>
            {
                options.CachePath = Path.Combine(dataFolder, fileName);
            });
        }
        else if (steamOptions.CacheProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDistributedSqlServerCache(options =>
            {
                options.ConnectionString = steamOptions.ConnectionString;
                options.SchemaName = steamOptions.CacheSchema;
                options.TableName = steamOptions.CacheTable;
            });
        }
        else
        {
            throw new InvalidOperationException($"Unsupported CacheProvider: {steamOptions.CacheProvider}. Use 'SQLite' or 'SqlServer'.");
        }

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
        var rateLimitLimit = config.GetValue<int>("RateLimiting:PermitLimit", 20);
        var rateLimitWindow = config.GetValue<int>("RateLimiting:WindowSeconds", 10);

        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("steam_api_limiter", limiterOptions =>
            {
                limiterOptions.Window = TimeSpan.FromSeconds(rateLimitWindow); // 10 second window
                limiterOptions.PermitLimit = rateLimitLimit;                  // Max 20 requests per window
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

            throw new InvalidOperationException(
                $"CRITICAL STARTUP FAILURE: Invalid Steam API Key detected. " +
                $"The current key length is {steamOptions.ApiKey?.Length ?? 0}. " +
                $"Ensure the Steam__ApiKey environment variable is set and IIS has been reset.");
        }

        return services;
    }
}