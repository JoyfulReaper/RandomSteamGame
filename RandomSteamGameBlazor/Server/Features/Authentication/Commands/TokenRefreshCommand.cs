using MediatR;
using RandomSteamGameBlazor.Server.Features.Authentication.Common;

namespace RandomSteamGameBlazor.Server.Features.Authentication.Commands;

public record TokenRefreshCommand(
    string token,
    string refreshToken) : IRequest<AuthenticationResult>;