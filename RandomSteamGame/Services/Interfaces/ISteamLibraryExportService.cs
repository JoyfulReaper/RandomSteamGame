using RandomSteamGame.Shared.Contracts;
using SteamDeckCompatibilityCategory = SteamApiClient.Contracts.SteamApi.SteamDeckCompatibilityCategory;

namespace RandomSteamGame.Services.Interfaces;

public interface ISteamLibraryExportService
{
    byte[] Export(
        OwnedGamesResponse library,
        IReadOnlyDictionary<int, SteamDeckCompatibilityCategory> steamDeckCompatibility);
}