/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Mapster;
using RandomSteamGameBlazor.Server.Features.Authentication.Common;
using RandomSteamGameBlazor.Shared.Contracts.Authentication;

namespace RandomSteamGameBlazor.Server.Common.Mapping;

public class AuthenticationMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        // We only need to map the internal Result to the outward-facing Response now
        config.NewConfig<AuthenticationResult, AuthenticationResponse>()
            .Map(dest => dest.Id, src => src.User.Id)
            .Map(dest => dest, src => src.User);
    }
}