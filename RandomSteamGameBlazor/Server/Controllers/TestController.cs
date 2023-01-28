using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace RandomSteamGameBlazor.Server.Controllers;
[Route("api/[controller]")]
[ApiController]
public class TestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("test");
    }
}
