using RandomSteamGame.SteamStoreApiContracts;

namespace RandomSteamGame.ViewModels;

public class RandomGameViewModel
{
    public Int64 SteamId { get; set; }
    public string? CustomUrl { get; set; }
    public AppDetails AppDetails { get; set; } = default!;
}
