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
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        if (allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException("No CORS origins configured.");
        }

        options.AddPolicy("DefaultCors", policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .WithMethods("GET", "POST")
                  .WithHeaders("Content-Type", "Authorization")
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

        app.UseHttpsRedirection();
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