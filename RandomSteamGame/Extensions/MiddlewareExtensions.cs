/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.AspNetCore.HttpOverrides;

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

        app.UseStaticFiles();
        app.UseAntiforgery();

        app.UseRateLimiter();

        return app;
    }
}
