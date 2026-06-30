/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.JRData.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Controllers;

[Route("api/stats")]
[ApiController]
public class HitController : ApiController
{
    private readonly SqliteConnection _dbConnection;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<HitController> _logger;

    public HitController(
        SqliteConnection dbConnection,
        IHttpContextAccessor httpContextAccessor,
        ILogger<HitController> logger)
    {
        _dbConnection = dbConnection;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    [HttpPost("hit")]
    public async Task<IActionResult> RecordHit()
    {
        var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        try
        {
            var stats = await HitCountHelper.ProcessHitCounts(_dbConnection, ip);
            return Ok(new AppStatsResponse(stats.totalHits, stats.uniqueVisitors));
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
            if (_dbConnection.State != System.Data.ConnectionState.Open)
            {
                await _dbConnection.OpenAsync();
            }

            var stats = await HitCountHelper.GetHitCounts(_dbConnection);
            return Ok(new AppStatsResponse(stats.totalHits, stats.uniqueVisitors));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch site hit statistics.");
            return Problem();
        }
    }
}
