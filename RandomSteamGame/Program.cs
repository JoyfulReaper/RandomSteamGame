/*
 * Random Steam Game
 * * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.AspNetCore.HttpOverrides;
using NeoSmart.Caching.Sqlite;
using RandomSteamGame.Components;
using SteamApiClient; //.AddSteamApiClient()

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// SERVICES CONFIGURATION (DI CONTAINER)
// ==========================================

// Blazor Interactive Auto Render Mode Engines
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();


builder.Services.AddSteamApiClient(builder.Configuration);
var cacheProvider = builder.Configuration.GetValue<string>("CacheProvider");

if (cacheProvider?.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
{
    var dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
    Directory.CreateDirectory(dataFolder); // Ensure the folder exists

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

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();
app.MapStaticAssets();

// ==========================================
// ENDPOINTS & ROUTING
// ==========================================

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(RandomSteamGame.Client._Imports).Assembly);

app.Run();