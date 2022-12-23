using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RandomSteamGame.Services;

namespace RandomSteamGame.Controllers;

[Route("api/[controller]")]
[ApiController]
public class GameController : ControllerBase
{
    private readonly SteamService _steamService;

    public GameController(SteamService steamService)
    {
        _steamService = steamService;
    }

    [HttpGet("RandomGame")]
    public async Task<IActionResult> RandomGame(Int64 steamId)
    {
        var randomGame = await _steamService.GetRandomGame(steamId);
        return Ok(randomGame);
    }
}
