/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.AspNetCore.HttpOverrides;
using Mythetech.LocalStorage;
using NeoSmart.Caching.Sqlite;
using RandomSteamGame.Components;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Interfaces;
using SteamApiClient; //.AddSteamApiClient()

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
var cacheProvider = builder.Configuration.GetValue<string>("CacheProvider");
if (cacheProvider?.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
{
    var dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
    Directory.CreateDirectory(dataFolder);

    builder.Services.AddSqliteCache(options =>
    {
        options.CachePath = Path.Combine(dataFolder, "cache.db");
    });
}
else if (cacheProvider?.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) == true)
{
    builder.Services.AddDistributedSqlServerCache(options =>
    {
        options.ConnectionString = builder.Configuration.GetConnectionString("SqlServerConnection");
        options.SchemaName = "dbo";
        options.TableName = "Cache";
    });
}
else
{
    throw new InvalidOperationException("Valid 'CacheProvider' (Sqlite or SqlServer) must be configured.");
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

var app = builder.Build();

// ==========================================
// HTTP REQUEST PIPELINE (MIDDLEWARE)
// ==========================================

if (app.Environment.IsDevelopment())
{
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

// ==========================================
// ENDPOINTS & ROUTING
// ==========================================

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(RandomSteamGame.Client._Imports).Assembly);

app.Run();