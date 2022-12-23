using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RandomSteamGame.Services;

namespace RandomSteamGame.Pages;

public class IndexModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ErrorMessage { get; set; }

    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ILogger<IndexModel> logger, SteamClient steamService)
    {
        _logger = logger;
    }

    public void OnGet()
    {
        if (TempData["ErrorMessage"] is not null)
        {
            ErrorMessage = TempData["ErrorMessage"]?.ToString() ?? "Unkown Error";
        }
    }
}