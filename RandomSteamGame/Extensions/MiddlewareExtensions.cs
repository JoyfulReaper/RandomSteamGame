/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using RandomSteamGame.Options;
using RandomSteamGame.Services;
using System.Net;
using System.Text.Json;

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

        var settings = app.ApplicationServices.GetRequiredService<IOptions<ApplicationOptions>>().Value;
        var ingress = app.ApplicationServices.GetRequiredService<IOptions<IngressOptions>>().Value;
        var enableForwarding = ingress.EnableForwardedHeaders ?? settings.NetworkMode == NetworkMode.Public;
        var enableCloudflare = ingress.EnableCloudflareVisitorHeader ?? settings.NetworkMode == NetworkMode.Public;
        var trustedProxies = (ingress.TrustedProxies ?? ["127.0.0.1", "::1"]).Select(IPAddress.Parse)
            .Select(address => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToArray();

        var forwardedOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1
        };
        forwardedOptions.KnownIPNetworks.Clear();
        forwardedOptions.KnownProxies.Clear();
        foreach (var proxy in trustedProxies)
        {
            forwardedOptions.KnownProxies.Add(proxy);
        }

        // Check the immediate peer before any forwarding changes RemoteIpAddress.
        // A trusted ingress must overwrite visitor-supplied forwarding headers.
        app.Use((context, next) =>
        {
            var peer = context.Connection.RemoteIpAddress;
            if (peer?.IsIPv4MappedToIPv6 == true)
            {
                peer = peer.MapToIPv4();
            }
            var trusted = enableForwarding && peer is not null && trustedProxies.Contains(peer);
            if (!trusted)
            {
                context.Request.Headers.Remove("X-Forwarded-For");
                context.Request.Headers.Remove("X-Forwarded-Proto");
            }
            else if (enableCloudflare && context.Request.Headers.TryGetValue("CF-Visitor", out var cfVisitor))
            {
                try
                {
                    using var visitor = JsonDocument.Parse(cfVisitor.ToString());
                    if (visitor.RootElement.ValueKind == JsonValueKind.Object &&
                        visitor.RootElement.TryGetProperty("scheme", out var scheme) &&
                        scheme.ValueKind == JsonValueKind.String && scheme.GetString() == "https")
                    {
                        context.Request.Headers["X-Forwarded-Proto"] = "https";
                    }
                }
                catch (JsonException) { /* Ignore malformed optional Cloudflare metadata. */ }
            }
            context.Request.Headers.Remove("CF-Visitor");
            return next();
        });

        app.UseForwardedHeaders(forwardedOptions);

        var cookiePolicy = app.ApplicationServices.GetRequiredService<DeploymentCookiePolicy>();
        if (cookiePolicy.IsAltNet)
        {
            app.Use((context, next) =>
            {
                // External scheme is deployment configuration, independent of tunnel headers.
                context.Request.Scheme = cookiePolicy.ExternalScheme;
                return next();
            });
        }

        var canonicalUrls = app.ApplicationServices.GetRequiredService<CanonicalUrlService>();
        app.Use(async (context, next) =>
        {
            if (canonicalUrls.IsBetaHost(context.Request.Host.Host) ||
                context.Request.Path.StartsWithSegments(
                    "/api",
                    StringComparison.OrdinalIgnoreCase) ||
                context.Request.Path.StartsWithSegments(
                    "/health",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    context.Request.Path.Value,
                    "/error",
                    StringComparison.OrdinalIgnoreCase))
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
