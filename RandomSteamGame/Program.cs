/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Mythetech.LocalStorage;
using NeoSmart.Caching.Sqlite;
using RandomSteamGame.Components;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Interfaces;
using SteamApiClient;
using SteamApiClient.Settings;
using System.Threading.RateLimiting; //.AddSteamApiClient()

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// SERVICES CONFIGURATION (DI CONTAINER)
// ==========================================

// Blazor Interactive Auto Render Mode Engines
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();

builder.Services.AddSteamApiClient(builder.Configuration); // Steam Api Client
builder.Services.AddScoped<ISteamIdentityService, ServerSteamIdentityService>();
builder.Services.AddScoped<ISteamService, SteamService>();
builder.Services.AddScoped<IHtmlSanitizerService, HtmlSanitizerService>();

builder.Services.AddLocalStorage(); // TODO: Think about possibly rolling our own or finding a different solution

// Cache Provider
var steamOptions = builder.Configuration.GetSection("Steam").Get<SteamClientApiOptions>()
                   ?? throw new InvalidOperationException("Steam configuration is missing.");

// Cache Provider Logic
if (steamOptions.CacheProvider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
{
    // Extract filename from "Data Source=filename.db"
    var fileName = steamOptions.ConnectionString.Replace("Data Source=", "", StringComparison.OrdinalIgnoreCase).Trim();
    var dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
    Directory.CreateDirectory(dataFolder);

    builder.Services.AddSqliteCache(options =>
    {
        options.CachePath = Path.Combine(dataFolder, fileName);
    });
}
else if (steamOptions.CacheProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddDistributedSqlServerCache(options =>
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
builder.Services.AddCors(options =>
{
    var configuredOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    var allowedOrigins = new List<string>(configuredOrigins);

    // Dynamically inject local development tools if running locally
    if (builder.Environment.IsDevelopment())
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
var rateLimitLimit = builder.Configuration.GetValue<int>("RateLimiting:PermitLimit", 10);
var rateLimitWindow = builder.Configuration.GetValue<int>("RateLimiting:WindowSeconds", 10);

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("steam_api_limiter", limiterOptions =>
    {
        limiterOptions.Window = TimeSpan.FromSeconds(rateLimitWindow); // 10 second window
        limiterOptions.PermitLimit = rateLimitLimit;                  // Max 10 requests per window
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

// DEBUG
var steamSection = builder.Configuration.GetSection("Steam");
var key = steamSection["ApiKey"];
Console.WriteLine($"DEBUG: API Key length is {key?.Length ?? 0}");

var app = builder.Build();

// ==========================================
// HTTP REQUEST PIPELINE (MIDDLEWARE)
// ==========================================

if (app.Environment.IsDevelopment())
{
    //app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseHsts();
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

// Cloudflare Tunnel Header Matching Middleware
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownIPNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
forwardedOptions.KnownProxies.Add(System.Net.IPAddress.Loopback);
forwardedOptions.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);

// Parse Cloudflare's specific schema declaration
app.Use((context, next) =>
{
    if (context.Request.Headers.TryGetValue("CF-Visitor", out var cfVisitor))
    {
        if (cfVisitor.ToString().Contains("\"scheme\":\"https\""))
        {
            context.Request.Headers["X-Forwarded-Proto"] = "https";
        }
    }
    return next();
});

app.UseForwardedHeaders(forwardedOptions);
app.UseCors("DefaultCors");
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();
app.MapStaticAssets();
app.UseRateLimiter();

// ==========================================
// ENDPOINTS & ROUTING
// ==========================================

app.MapControllers();

// For testing custom 500 page
//app.MapGet("/trigger-500", () =>
//{
//    throw new Exception("You wanted a 500? ok!");
//});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(RandomSteamGame.Client._Imports).Assembly);

app.Run();