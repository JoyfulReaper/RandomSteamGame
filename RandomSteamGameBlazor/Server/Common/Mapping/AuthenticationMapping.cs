using Mapster;
using RandomSteamGameBlazor.Server.Features.Authentication.Commands;
using RandomSteamGameBlazor.Server.Features.Authentication.Common;
using RandomSteamGameBlazor.Server.Features.Authentication.Queries;
using RandomSteamGameBlazor.Shared.Contracts.Authentication;

namespace RandomSteamGameBlazor.Server.Common.Mapping;

public class AuthenticationMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<RegisterRequest, RegisterCommand>();

        config.NewConfig<LoginRequest, LoginQuery>();

        config.NewConfig<TokenRefreshRequest, TokenRefreshCommand>();

        config.NewConfig<AuthenticationResult, AuthenticationResponse>()
            .Map(dest => dest.Id, src => src.User.Id)
            .Map(dest => dest, src => src.User);
    }
}
