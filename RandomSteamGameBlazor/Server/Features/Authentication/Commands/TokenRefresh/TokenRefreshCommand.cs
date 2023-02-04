using ErrorOr;
using MediatR;
using RandomSteamGameBlazor.Server.Features.Authentication.Common;

namespace RandomSteamGameBlazor.Server.Features.Authentication.Commands.TokenRefresh;

public record TokenRefreshCommand(
    string token,
    string refreshToken) : IRequest<ErrorOr<AuthenticationResult>>;