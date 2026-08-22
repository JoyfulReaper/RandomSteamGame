/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.AspNetCore.HttpOverrides;
using RandomSteamGame.Services;

namespace RandomSteamGame.Extensions;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder ConfigurePipeline(
        this IApplicationBuilder app,
        IHostEnvironment env)
    {
        // ==========================================
        // HTTP REQUEST PIPELINE (MIDDLEWARE)
        // ==========================================

        if (env.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
            //app.UseExceptionHandler("/Error", createScopeForErrors: true);
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

        var canonicalUrls = app.ApplicationServices.GetRequiredService<CanonicalUrlService>();
        app.Use(async (context, next) =>
        {
            if (canonicalUrls.IsBetaHost(context.Request.Host.Host))
            {
                context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            }
            else if (context.Request.Path.StartsWithSegments(
                         "/random-game",
                         StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Headers["X-Robots-Tag"] = "noindex, follow";
            }

            await next();
        });

        app.UseCors("DefaultCors");
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

        app.UseStaticFiles();
        app.UseAntiforgery();

        app.UseRateLimiter();

        return app;
    }
}
