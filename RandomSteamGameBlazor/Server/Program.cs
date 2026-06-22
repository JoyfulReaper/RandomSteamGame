/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using RandomSteamGameBlazor.Server;
using RandomSteamGameBlazor.Server.Services;
using SteamApiClient;

var builder = WebApplication.CreateBuilder(args);
{
    builder.Services.AddControllersWithViews();
    builder.Services.AddRazorPages();

    builder.Services.AddScoped<ISteamService, SteamService>();
    builder.Services.AddScoped<IAuthService, AuthService>();

    builder.Services.AddRandomSteamGame(builder.Configuration);
    builder.Services.AddSteamApiClient(builder.Configuration);

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
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseHsts();
            app.UseExceptionHandler("/error");
        }

        //app.UseHttpsRedirection(); // We don't need this, the Cloudflare proxy takes care of it
        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();

        app.UseRouting();
        app.UseCors("DefaultCors");

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapRazorPages();
        app.MapControllers();
        app.MapFallbackToFile("index.html");

        app.Run();
    }
}