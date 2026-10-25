using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Controllers;

[Route("api/features")]
[AllowAnonymous]
[DisableCors]
[EnableRateLimiting(FeatureVotingRateLimits.PolicyName)]
public sealed class FeaturesController(
    IFeatureVotingService votes, FeatureVotingIdentity identity, ILogger<FeaturesController> logger) : ApiController
{
    [HttpGet]
    public Task<IActionResult> Get() => ExecuteAsync(async () =>
        Ok(await votes.GetAsync(identity.GetOrCreateHash(HttpContext), HttpContext.RequestAborted)));

    [HttpPost("{id}/vote")]
    [Consumes("application/json")]
    [RequestSizeLimit(1024)]
    [RequireAntiforgeryToken]
    public Task<IActionResult> Vote([Range(1, long.MaxValue)] long id, [FromBody] FeatureVoteRequest request) =>
        ExecuteAsync(async () =>
        {
            // Mutations never silently replace a missing/invalid identity. Bootstrap with GET first.
            var hash = identity.ReadHash(HttpContext);
            if (hash is null)
                return Problem(statusCode: 400, detail: "Load the feature board before voting.");
            var result = await votes.SetSupportedAsync(id, hash, request.Supported!.Value, HttpContext.RequestAborted);
            return result switch
            {
                FeatureVoteResult.NotFound => Problem(statusCode: 404, detail: "Feature proposal not found."),
                FeatureVoteResult.Closed => Problem(statusCode: 409, detail: "This proposal is not open for voting."),
                _ => NoContent()
            };
        });

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> operation)
    {
        try { return await operation(); }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is SqliteException or TimeoutException)
        {
            logger.LogWarning(exception, "Feature voting database is unavailable.");
            Response.Headers.RetryAfter = "1";
            return Problem(statusCode: 503, detail: "Feature voting is temporarily unavailable. Please try again.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Feature voting request failed.");
            return Problem(statusCode: 500, detail: "Unable to complete the feature voting request.");
        }
    }
}
