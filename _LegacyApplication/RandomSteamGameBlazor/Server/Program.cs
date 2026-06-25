/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.AspNetCore.HttpOverrides;
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
            //app.UseWebAssemblyDebugging();
            app.UseExceptionHandler("/error");
        }
        else
        {
            app.UseHsts();
            app.UseExceptionHandler("/error");
        }

        // Cloudfare Configuration
        var forwardedOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        };

        forwardedOptions.KnownIPNetworks.Clear();
        forwardedOptions.KnownProxies.Clear();

        // Explicitly trust the local loopback adapters so local proxy headers are respected
        forwardedOptions.KnownProxies.Add(System.Net.IPAddress.Loopback);
        forwardedOptions.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);

        // Cloudflare CF-Visitor parsing middleware
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

        // hand off the modified headers to the native Forwarded Headers middleware
        app.UseForwardedHeaders(forwardedOptions);

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

        app.MapGet("/crash", () =>
        {
            throw new Exception("Server-side catastrophe!");
        });

        // Custom 500 page
        app.MapGet("/error", async context =>
        {
            context.Response.ContentType = "text/html";
            context.Response.StatusCode = 500;

            await context.Response.SendFileAsync("wwwroot/error.html");
        });

        app.Run();
    }
}