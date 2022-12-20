using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RandomSteamGame.Services;

namespace RandomSteamGame.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly SteamService _steamService;

    public IndexModel(ILogger<IndexModel> logger, SteamService steamService)
    {
        _logger = logger;
        _steamService = steamService;
    }

    public void OnGet()
    {
    }
}