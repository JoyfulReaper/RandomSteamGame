using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RandomSteamGame.Exceptions;
using RandomSteamGame.Services;
using RandomSteamGame.SteamApiContracts;
using RandomSteamGame.SteamStoreApiContracts;

namespace RandomSteamGame.Pages
{
    public class RandomGameModel : PageModel
    {
        [BindProperty]
        public Int64? SteamId { get; set; }

        [BindProperty]
        public string? CustomUrl { get; set; }

        public AppDetailsResponse AppDetails { get; set; } = new();
        public Game Game { get; set; } = default!;
        public string? ErrorMessage { get; set; } = null;

        private readonly SteamService _steamService;
        private readonly SteamStoreService _steamStoreService;
        private readonly ILogger<RandomGameModel> _logger;

        public RandomGameModel(
            SteamService steamService, 
            SteamStoreService steamStoreService,
            ILogger<RandomGameModel> logger)
        {
            _steamService = steamService;
            _steamStoreService = steamStoreService;
            _logger = logger;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPost()
        {
            if (SteamId is null && CustomUrl is null)
            {
                ErrorMessage = "Please enter a Steam ID or Custom URL";
                TempData["ErrorMessage"] = ErrorMessage;
                return RedirectToPage("Index");
            }

            if (CustomUrl is not null && SteamId is null)
            {
                try
                {
                    SteamId = await _steamService.GetSteamIdFromVanityUrl(CustomUrl);
                }
                catch (VanityResolutionException)
                {
                    ErrorMessage = $"Could not find a Steam ID for the custom URL: '{CustomUrl}'";
                    TempData["ErrorMessage"] = ErrorMessage;
                    return RedirectToPage("Index");
                }
            }


            //76561197988408972
            var gamesOwned = await _steamService.GetOwnedGames(SteamId.Value);

            int attempts = 0;
            while(!AppDetails.Success)
            {
                Game = gamesOwned.Games[Random.Shared.Next(0, gamesOwned.GameCount - 1)];
                AppDetails = await _steamStoreService.GetAppData(Game.AppId);

                if(!AppDetails.Success)
                {
                    // TODO: Clean this up. We don't want an infinte loop if no app data can be retreived
                    attempts++;
                    if(attempts >= 3)
                    {
                        throw new Exception("Unable to get app details");
                    }
                    _logger.LogWarning("Unable to get app details for {AppId}", Game.AppId);
                }
            }

            return Page();
        }
    }
}
