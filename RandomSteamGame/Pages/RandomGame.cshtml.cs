using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RandomSteamGame.Services;
using RandomSteamGame.SteamApiContracts;
using RandomSteamGame.SteamStoreApiContracts;

namespace RandomSteamGame.Pages
{
    public class RandomGameModel : PageModel
    {
        [BindProperty]
        public Int64 SteamId { get; set; } = default!;

        public AppDetailsResponse AppDetails { get; set; }
        public Game Game { get; set; }

        private readonly SteamService _steamService;
        private readonly SteamStoreService _steamStoreService;

        public RandomGameModel(SteamService steamService, SteamStoreService steamStoreService)
        {
            _steamService = steamService;
            _steamStoreService = steamStoreService;
        }

        public void OnGet()
        {
        }

        public async Task OnPost()
        {
            //76561197988408972
            var gamesOwned = await _steamService.GetOwnedGames(SteamId);

            Game = gamesOwned.Games[Random.Shared.Next(0, gamesOwned.GameCount -1)];
            AppDetails = await _steamStoreService.GetAppData(Game.AppId);
        }
    }
}
