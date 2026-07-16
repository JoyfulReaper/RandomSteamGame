/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RandomSteamGame.Services.Interfaces;

namespace RandomSteamGame.Controllers;

[Route("api/stats")]
[ApiController]
[EnableRateLimiting("steam_api_limiter")]
public class HitController : ApiController
{
    private readonly IAppStatsService _appStatsService;
    private readonly ILogger<HitController> _logger;

    public HitController(
        IAppStatsService appStatsService,
        ILogger<HitController> logger)
    {
        _appStatsService = appStatsService;
        _logger = logger;
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
