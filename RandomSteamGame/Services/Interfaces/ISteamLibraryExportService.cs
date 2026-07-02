using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Services.Interfaces;

public interface ISteamLibraryExportService
{
    byte[] Export(OwnedGamesResponse library);
}
