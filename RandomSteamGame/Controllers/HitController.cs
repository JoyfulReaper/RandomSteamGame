/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.MissionControl;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RandomSteamGame.Events;
using RandomSteamGame.Services.Interfaces;
using System.Diagnostics;

namespace RandomSteamGame.Controllers;

[Route("api/stats")]
[ApiController]
[EnableRateLimiting("steam_api_limiter")]
public class HitController : ApiController
{
    private readonly IAppStatsService _appStatsService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMissionControlClient _missionControlClient;
    private readonly ILogger<HitController> _logger;

    public HitController(
        IAppStatsService appStatsService,
        IHttpContextAccessor httpContextAccessor,
        IMissionControlClient missionControlClient,
        ILogger<HitController> logger)
    {
        _appStatsService = appStatsService;
        _httpContextAccessor = httpContextAccessor;
        _missionControlClient = missionControlClient;
        _logger = logger;
    }

    [HttpPost("hit")]
    public async Task<IActionResult> RecordHit()
    {
        var occurredAt = DateTimeOffset.UtcNow;
        var correlationId = Guid.NewGuid().ToString("N");
        var hitStopwatch = Stopwatch.StartNew();

        var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        try
        {
            var stats = await _appStatsService.RecordHitAsync(ip);

            try
            {
                await _missionControlClient.TryPublishAsync(
                    eventType: RandomSteamGameEventTypes.SiteVisitRecorded,
                    payload: new SiteVisitRecordedEvent(
                        TotalHits: stats.TotalHits,
                        UniqueVisitors: stats.UniqueVisitors,
                        DurationMilliseconds: hitStopwatch.ElapsedMilliseconds),
                    occurredAt: occurredAt,
                    correlationId: correlationId,
                    cancellationToken: CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to publish site visit event {CorrelationId}.",
                    correlationId);
            }
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record site hit.");
            return Problem();
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var stats = await _appStatsService.GetStatsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch site hit statistics.");
            return Problem();
        }
    }
}
