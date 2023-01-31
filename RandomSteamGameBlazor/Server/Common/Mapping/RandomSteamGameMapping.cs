using Mapster;
using RandomSteamGameBlazor.Shared.Contracts.RandomSteamGame;

namespace RandomSteamGameBlazor.Server.Common.Mapping;

public class RandomSteamGameMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig < (SteamApiClient.Contracts.SteamApi.OwnedGames Response, long SteamId), OwnedGamesResponse>()
            .Map(dest => dest.SteamId, src => src.SteamId)
            .Map(dest => dest, src => src.Response);
    }
}