
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
            // Steam authentication is available only on the public HTTPS deployment.
            if (applicationOptions.Value.NetworkMode != NetworkMode.Public ||
                !context.Request.IsHttps)
            {
                return Results.NotFound();
            }

            if (context.User.Identity?.IsAuthenticated == true)
                return Results.Redirect("/auth/me");

            return Results.Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = "/auth/me"
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

        return endpoints;
    }
}
