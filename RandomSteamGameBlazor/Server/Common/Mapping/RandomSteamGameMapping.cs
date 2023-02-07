using Mapster;
using RandomSteamGameBlazor.Shared.Contracts.RandomSteamGame;
using SteamApiClient.Contracts.SteamStoreApi;

namespace RandomSteamGameBlazor.Server.Common.Mapping;

public class RandomSteamGameMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig <(SteamApiClient.Contracts.SteamApi.OwnedGames Response, long SteamId), OwnedGamesResponse>()
            .Map(dest => dest.SteamId, src => src.SteamId)
            .Map(dest => dest, src => src.Response);

        config.NewConfig<(OwnedGamesResponse OwnedGames, AppData GameData), RandomGameResponse>()
            .Map(dest => dest.SteamId, src => src.OwnedGames.SteamId)
            .Map(dest => dest.AppId, src => src.GameData.SteamAppId)
            .Map(dest => dest.AppInformation, src => src.GameData)
            .Map(dest => dest, src => src.OwnedGames.Games.Find(g => g.AppId == src.GameData.SteamAppId));
    }
}