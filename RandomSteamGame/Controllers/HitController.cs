/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.JRData.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace RandomSteamGame.Controllers;

[Route("api/stats")]
[ApiController]
public class HitController : ApiController
{
    private readonly SqliteConnection _dbConnection;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HitController(SqliteConnection dbConnection, IHttpContextAccessor httpContextAccessor)
    {
        _dbConnection = dbConnection;
        _httpContextAccessor = httpContextAccessor;
    }

    [HttpPost("hit")]
    public async Task<IActionResult> RecordHit()
    {
        var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        try
        {
            var stats = await HitCountHelper.ProcessHitCounts(_dbConnection.ConnectionString, ip);
            return Ok(new { TotalHits = stats.totalHits, UniqueVisitors = stats.uniqueVisitors });
        }
        catch (Exception)
        {
            return StatusCode(500);
        }
    }
}