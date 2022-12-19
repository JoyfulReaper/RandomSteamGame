using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RandomSteamGame.Services;

namespace RandomSteamGame.Pages;
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly SteamStoreService _steamService;

    public IndexModel(ILogger<IndexModel> logger,
        SteamStoreService steamService)
    {
        _logger = logger;
        _steamService = steamService;
    }

    public async Task OnGetAsync()
    {
        var test = await _steamService.GetAppData(340);
        //var test = await _steamService.GetOwnedGames(76561197988408972);
    }
}
