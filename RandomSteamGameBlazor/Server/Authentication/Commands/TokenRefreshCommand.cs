using MediatR;
using RandomSteamGameBlazor.Server.Authentication.Common;

namespace RandomSteamGameBlazor.Server.Authentication.Commands;

public record TokenRefreshCommand(
    string token,
    string refreshToken) : IRequest<AuthenticationResult>;