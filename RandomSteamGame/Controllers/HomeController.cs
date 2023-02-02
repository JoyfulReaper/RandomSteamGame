using Microsoft.AspNetCore.Mvc;
using RandomSteamGame.Models;
using RandomSteamGame.Services;
using SteamApiClient.Exceptions;
using SteamApiClient.HttpClients;
using System.Diagnostics;

namespace RandomSteamGame.Controllers;
public class HomeController : Controller
{
    private readonly SteamService _steamService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        SteamService steamService,
        ILogger<HomeController> logger)
    {
        _steamService = steamService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult RandomGame()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> RandomGame(long? steamId, string? customUrl)
    {
        try
        {
            if (steamId is null && customUrl is null)
            {
                TempData["ErrorMessage"] = "Please enter a Steam ID or Custom URL";
                return RedirectToAction("Index");
            }
            
            if (customUrl is not null && steamId is null)
            {
                steamId = await _steamService.GetSteamIdFromVanityUrl(customUrl);
                if(steamId is null)
                {
                    throw new VanityResolutionException();
                }
            }

            return View(new RandomGameViewModel { CustomUrl = customUrl, SteamId = steamId!.Value });
        }
        catch (VanityResolutionException)
        {
            string errorMessage = $"Could not find a Steam ID for the custom URL: '{customUrl}'";
            TempData["ErrorMessage"] = errorMessage;
            return RedirectToAction("Index");
        }
    }

    [HttpGet]
    public async Task<IActionResult> RandomGameData(long steamId)
    {
        // TODO MAKE THIS A POST AND REQUIRE ANTIFORGERTY
        var details = await _steamService.GetRandomGame(steamId);
        return new JsonResult(details);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
