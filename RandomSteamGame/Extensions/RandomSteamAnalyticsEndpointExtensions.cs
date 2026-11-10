
using Microsoft.Extensions.Options;
using RandomSteamGame.Options;
using RandomSteamGame.Services.Analytics;
using System.Text.Json;

namespace RandomSteamGame.Extensions;

public static class RandomSteamAnalyticsEndpointExtensions
{
    public static IEndpointRouteBuilder MapRandomSteamAnalyticsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/admin/analytics")
            .RequireAuthorization("SteamAdmin");

        group.MapGet("/overview", async (
            int? days,
            HttpContext context,
            RandomSteamAnalyticsClient client,
            IOptions<RandomSteamAnalyticsOptions> options,
            ILogger<RandomSteamAnalyticsClient> logger,
            CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "private, no-store";

            int effectiveDays = days ?? 30;

            if (effectiveDays is < 1 or > 90)
                return Results.BadRequest("Days must be between 1 and 90.");

            if (!options.Value.Enabled)
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            try
            {
                var overview = await client.GetOverviewAsync(
                    effectiveDays, cancellationToken);

                return Results.Ok(overview);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Mission Control analytics request timed out.");
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            catch (HttpRequestException exception)
            {
                logger.LogWarning(
                    "Mission Control analytics request failed. Status: {StatusCode}",
                    exception.StatusCode);

                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            catch (JsonException)
            {
                logger.LogWarning("Mission Control returned invalid analytics JSON.");
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
            catch (InvalidOperationException)
            {
                logger.LogWarning("Mission Control returned an invalid analytics response.");
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        })
        .WithName("GetAdminRandomSteamAnalyticsOverview")
        .WithTags("Admin Analytics");

        return endpoints;
    }
}
