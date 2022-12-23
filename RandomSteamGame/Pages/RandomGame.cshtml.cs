using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RandomSteamGame.Exceptions;
using RandomSteamGame.Services;


namespace RandomSteamGame.Pages
{
    public class RandomGameModel : PageModel
    {
        [BindProperty]
        public Int64? SteamId { get; set; }

        [BindProperty]
        public string? CustomUrl { get; set; }

        public string? ErrorMessage { get; set; } = null;

        private readonly SteamClient _steamClient;
        private readonly SteamStoreClient _steamStoreClient;
        private readonly SteamService _steamService;
        private readonly ILogger<RandomGameModel> _logger;

        public RandomGameModel(
            SteamClient steamClient,
            SteamStoreClient steamStoreClient,
            SteamService steamService,
            ILogger<RandomGameModel> logger)
        {
            _steamClient = steamClient;
            _steamStoreClient = steamStoreClient;
            _steamService = steamService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGet(Int64 steamId)
        {
            var details = await _steamService.GetRandomGame(steamId);
            return new JsonResult(details);
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
                    SteamId = await _steamClient.GetSteamIdFromVanityUrl(CustomUrl);
                }
                catch (VanityResolutionException)
                {
                    ErrorMessage = $"Could not find a Steam ID for the custom URL: '{CustomUrl}'";
                    TempData["ErrorMessage"] = ErrorMessage;
                    return RedirectToPage("Index");
                }
            }

            return Page();
        }
    }
}
