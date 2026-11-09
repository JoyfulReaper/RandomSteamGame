
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using RandomSteamGame.Options;
using System.Security.Claims;

namespace RandomSteamGame.Extensions;

public static class SteamAuthenticationEndpointExtensions
{
    public static IEndpointRouteBuilder MapSteamAuthenticationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth");

        group.MapGet("/login", (
            HttpContext context,
            IOptions<ApplicationOptions> applicationOptions) =>
        {
            if (applicationOptions.Value.NetworkMode != NetworkMode.Public ||
                !context.Request.IsHttps)
            {
                return Results.NotFound();
            }

            // Fixed destinations only; no user-supplied redirect URLs.
            var forPicker = context.Request.Query["picker"] == "1";
            var redirectUri = forPicker ? "/?useSteamAccount=1" : "/auth/me";

            if (context.User.Identity?.IsAuthenticated == true)
                return Results.Redirect(redirectUri);

            return Results.Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = redirectUri
                },
                authenticationSchemes: ["Steam"]);
        });

        group.MapGet("/me", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "private, no-store";

            var user = context.User;

            if (user.Identity?.IsAuthenticated != true)
                return Results.Ok(new { Authenticated = false });

            return Results.Ok(new
            {
                Authenticated = true,
                NameIdentifier = user.FindFirstValue(ClaimTypes.NameIdentifier),
                Name = user.Identity.Name
            });
        });

        group.MapPost("/logout", async (
            HttpContext context,
            IAntiforgery antiforgery) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest("Invalid logout request.");
            }

            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            context.Response.Headers.CacheControl = "private, no-store";
            return Results.Redirect("/");
        });


        group.MapGet("/steam-id", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "private, no-store";

            if (context.User.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();

            const string prefix = "https://steamcommunity.com/openid/id/";
            var identifier = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (identifier is null || !identifier.StartsWith(prefix, StringComparison.Ordinal))
                return Results.Unauthorized();

            var steamId = identifier[prefix.Length..];

            if (steamId.Length != 17 || !steamId.All(char.IsAsciiDigit))
                return Results.Unauthorized();

            return Results.Ok(new { SteamId = steamId });
        });


        return endpoints;
    }
}
