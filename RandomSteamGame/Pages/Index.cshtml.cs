using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RandomSteamGame.Services;

namespace RandomSteamGame.Pages;
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly SteamService _steamService;

    public IndexModel(ILogger<IndexModel> logger,
        SteamService steamService)
    {
        _logger = logger;
        _steamService = steamService;
    }

    public async Task OnGetAsync()
    {
        var test = await _steamService.GetOwnedGames(76561197988408972);
    }
}
