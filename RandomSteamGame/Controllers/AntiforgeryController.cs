using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace RandomSteamGame.Controllers;

[ApiController]
[Route("api/antiforgery")]
[DisableCors]
public sealed class AntiforgeryController(IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("token")]
    public IActionResult GetToken()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["CDN-Cache-Control"] = "no-store";
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { tokens.RequestToken, tokens.HeaderName });
    }
}
